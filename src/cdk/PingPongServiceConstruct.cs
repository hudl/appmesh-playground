using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.AppMesh;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.ServiceDiscovery;
using static Amazon.CDK.AWS.AppMesh.CfnRoute;

namespace cdk
{
    public sealed class PingPongServiceConstructProps : StackProps
    {
        public ContainerImage ServiceContainerImage { get; set; }
        public RepositoryImage EnvoyImage { get; set; }
        public IRole TaskExecutionRole { get; set; }
        public string ServiceName { get; set; }
        public string Branch { get; set; }
        public Mesh Mesh { get; set; }
        public PrivateDnsNamespace CloudmapNamespace { get; set; }
        public string VirturalNodeName { get; set; }
        public Cluster Cluster { get; set; }
        public IVpc Vpc { get; set; }
        public VirtualService[] Backends { get; set; }
        public VirtualRouter VirtualRouter { get; set; }
        public int RoutePriority { get; set; }

    }
    public class PingPongServiceConstruct : Construct
    {
        public FargateService FargateService { get; private set; }
        public Amazon.CDK.AWS.ServiceDiscovery.IService CloudmapService { get; set; }
        public PingPongServiceConstruct(Construct scope, string id, PingPongServiceConstructProps props) : base(scope, id)
        {
            // Create the service
            (FargateService, CloudmapService) = CreateService(id, props.ServiceName, props.Branch, props.Vpc, props.Cluster, props.CloudmapNamespace, props.VirturalNodeName, props.Mesh, props.TaskExecutionRole, props.EnvoyImage, props.ServiceContainerImage);
            // Add a virtualNode to the mesh for this service
            var virtualNode = AddVirtualNode(id, props.ServiceName, props.Branch, props.Mesh, CloudmapService, props.Backends);
            var virtualRoute = CreateRouterRoute(id, props.ServiceName, props.Branch, props.VirtualRouter, virtualNode, props.Mesh, props.RoutePriority);
        }

        private VirtualNode AddVirtualNode(string id, string virtualNodeServiceName, string branch, Mesh mesh, Amazon.CDK.AWS.ServiceDiscovery.IService cloudmapService, VirtualService[] backends = null)
        {
            var virtualNode = mesh.AddVirtualNode($"{virtualNodeServiceName}-{branch}-node", new VirtualNodeProps()
            {
                Mesh = mesh,
                VirtualNodeName = $"{virtualNodeServiceName}-service-{branch}-node",
                CloudMapService = cloudmapService,
                Listener = new VirtualNodeListener()
                {
                    PortMapping = new Amazon.CDK.AWS.AppMesh.PortMapping
                    {
                        Port = 5000,
                        Protocol = Amazon.CDK.AWS.AppMesh.Protocol.HTTP
                    }
                },
                Backends = backends
            });
            return virtualNode;
        }

        private Amazon.CDK.AWS.AppMesh.CfnRoute CreateRouterRoute(string id, string serviceName, string branch, VirtualRouter virtualRouter, VirtualNode virtualNode, Mesh mesh, int priority)
        {
            return new Amazon.CDK.AWS.AppMesh.CfnRoute(this, $"{serviceName}-{branch}-route", new Amazon.CDK.AWS.AppMesh.CfnRouteProps
            {
                MeshName = mesh.MeshName,
                VirtualRouterName = virtualRouter.VirtualRouterName,
                RouteName = $"{serviceName}-{branch}-route",
                Spec = new RouteSpecProperty
                {
                    Priority = priority,
                    HttpRoute = new HttpRouteProperty
                    {
                        Match = new HttpRouteMatchProperty
                        {
                            Headers = new HttpRouteHeaderProperty[]
                            {
                                new HttpRouteHeaderProperty {
                                    Name = "x-branch-header",
                                    Invert = false,
                                    Match = new HeaderMatchMethodProperty {
                                        Exact = $"{branch}"
                                    }
                                }
                            },
                            Prefix = "/"
                        },
                        Action = new HttpRouteActionProperty
                        {
                            WeightedTargets = new WeightedTargetProperty[]
                            {
                                new WeightedTargetProperty()
                                {
                                    VirtualNode = virtualNode.VirtualNodeName,
                                    Weight = 1
                                }
                            }
                        }
                    },
                }
            });

        }

        private (FargateService, Amazon.CDK.AWS.ServiceDiscovery.IService) CreateService(string id,
            string serviceName,
            string branch,
            IVpc vpc,
            Cluster cluster,
            PrivateDnsNamespace cloudMapNamespace,
            string appMeshVirtualNodeName,
            Mesh mesh,
            IRole taskExecutionRole,
            RepositoryImage envoyImage,
            ContainerImage containerImage)
        {
            var taskDefinition = new TaskDefinition(this, $"{id}_{serviceName}-{branch}-task-definiton", new TaskDefinitionProps
            {
                Compatibility = Compatibility.FARGATE,
                MemoryMiB = "512",
                Cpu = "256",
                ProxyConfiguration = new AppMeshProxyConfiguration(new AppMeshProxyConfigurationConfigProps()
                {
                    ContainerName = "envoy",
                    Properties = new AppMeshProxyConfigurationProps()
                    {
                        AppPorts = new double[1] { 5000 },
                        ProxyIngressPort = 15000,
                        ProxyEgressPort = 15001,
                        IgnoredUID = 1337,
                        EgressIgnoredIPs = new string[2] { "169.254.170.2", "169.254.169.254" }
                    }
                }),
                Family = $"{id}_{serviceName}-task-definition",
                ExecutionRole = taskExecutionRole
            });
            var envoyContainer = taskDefinition.AddContainer("envoy", new ContainerDefinitionOptions
            {
                User = "1337",
                Image = envoyImage,
                Essential = true,
                Environment = new Dictionary<string, string>
                {
                    {"APPMESH_VIRTUAL_NODE_NAME", $"mesh/{mesh.MeshName}/virtualNode/{appMeshVirtualNodeName}"}
                },
                HealthCheck = new Amazon.CDK.AWS.ECS.HealthCheck()
                {
                    Command = new string[] { "CMD-SHELL", "curl -s http://localhost:9901/server_info | grep state | grep -q LIVE" },
                    Interval = Duration.Seconds(5),
                    Timeout = Duration.Seconds(2),
                    StartPeriod = Duration.Seconds(10),
                    Retries = 3
                },
                MemoryLimitMiB = 500,
                Logging = new AwsLogDriver(new AwsLogDriverProps()
                {
                    StreamPrefix = $"{id}_{serviceName}-{branch}-envoy",
                    LogRetention = RetentionDays.ONE_DAY
                }),
            });
            var container = taskDefinition.AddContainer($"{serviceName}-container", new ContainerDefinitionOptions()
            {
                Image = containerImage,
                Logging = new AwsLogDriver(new AwsLogDriverProps()
                {
                    StreamPrefix = $"{id}_{serviceName}-{branch}-service",
                    LogRetention = RetentionDays.ONE_DAY
                }),
                Essential = true,
                Environment = new Dictionary<string, string>
                {
                    {"BRANCH", branch},
                    {"APPMESH_NAMESPACE", cloudMapNamespace.PrivateDnsNamespaceName}
                }
            });
            container.AddPortMappings(new Amazon.CDK.AWS.ECS.PortMapping()
            {
                ContainerPort = 5000
            });
            container.AddContainerDependencies(new ContainerDependency()
            {
                Condition = ContainerDependencyCondition.HEALTHY,
                Container = envoyContainer
            });
            // Cloudmap will append the namespace to the dns entry in R53. 
            // We're explicitly checking for master here because for service to service lookups to go via the envoy proxy, the DNS name must resolve. 
            // see https://github.com/aws/aws-app-mesh-roadmap/issues/65
            // i.e I want the ping service to call http://pong-service.{namespace}:5000/ and for this to be routed correctly by the proxy. 
            // If you create the fargate task with cloudmap service integration with a more specific (branched) DNS name then pong-service.{namespace} r53 entry will never be created 
            // and routing doesn't work through envoy. 
            var dnsName = $"{serviceName}-service{(branch == "master" ? "" : "-" + branch)}";
            var sg = new SecurityGroup(this, $"{id}_{serviceName}-{branch}-sg", new SecurityGroupProps()
            {
                AllowAllOutbound = true,
                SecurityGroupName = $"{id}_{serviceName}-{branch}-sg",
                Vpc = vpc,
            });
            sg.AddIngressRule(Peer.AnyIpv4(), new Port(new PortProps() { Protocol = Amazon.CDK.AWS.EC2.Protocol.TCP, FromPort = 5000, ToPort = 5000, StringRepresentation = "tcp:5000:5000" }), "allow access from outside.");
            var fargateService = new Amazon.CDK.AWS.ECS.FargateService(this, $"{serviceName}-{branch}-service", new FargateServiceProps
            {
                ServiceName = $"{serviceName}-{branch}-service",
                AssignPublicIp = true,
                Cluster = cluster,
                TaskDefinition = taskDefinition,
                VpcSubnets = new SubnetSelection() { Subnets = vpc.PublicSubnets },
                CloudMapOptions = new CloudMapOptions()
                {
                    Name = dnsName,
                    DnsRecordType = DnsRecordType.A,
                    CloudMapNamespace = cloudMapNamespace
                },
                SecurityGroup = sg
            });
            return (fargateService, fargateService.CloudMapService);

        }
    }
}