using Amazon.CDK;
using Amazon.CDK.AWS.AppMesh;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.ServiceDiscovery;

namespace cdk
{
    public class PingPongServices : Construct
    {
        public PingPongServices(Construct scope, string id, PingPongServicesProps props) : base(scope, id)
        {
            var pingImage = ContainerImage.FromAsset($"ping-service");
            var pongImage = ContainerImage.FromAsset($"pong-service");
            var envoyImage = ContainerImage.FromRegistry($"840364872350.dkr.ecr.{props.Env.Region}.amazonaws.com/aws-appmesh-envoy:v1.12.3.0-prod");
            // TODO: Maybe just create this role here as part of the stack. I think it gets created with the ECS cluster automatically. 
            var taskExecRole = Role.FromRoleArn(this, "task-execution-role", $"arn:aws:iam::{props.Env.Account}:role/ecsTaskExecutionRole");
            var pingService = new PingPongServiceConstruct(this, $"{id}_ping-master", new PingPongServiceConstructProps()
            {
                ServiceContainerImage = pingImage,
                EnvoyImage = envoyImage,
                TaskExecutionRole = taskExecRole,
                ServiceName = "ping",
                Branch = "master",
                Mesh = props.Mesh,
                CloudmapNamespace = props.CloudmapNamespace,
                VirturalNodeName = $"ping-service-master-node",
                Cluster = props.Cluster,
                Vpc = props.Vpc,
                Backends = new VirtualService[] { props.PongVirtualService },
                VirtualRouter = props.PingVirtualRouter,
                RoutePriority = 0
            });
            var pongMasterService = new PingPongServiceConstruct(this, $"{id}_pong-master", new PingPongServiceConstructProps()
            {
                ServiceContainerImage = pongImage,
                EnvoyImage = envoyImage,
                TaskExecutionRole = taskExecRole,
                ServiceName = "pong",
                Branch = "master",
                Mesh = props.Mesh,
                CloudmapNamespace = props.CloudmapNamespace,
                VirturalNodeName = $"pong-service-master-node",
                Cluster = props.Cluster,
                Vpc = props.Vpc,
                Backends = new VirtualService[] { },
                VirtualRouter = props.PongVirtualRouter,
                RoutePriority = 2
            });
            var pongBranchService = new PingPongServiceConstruct(this, $"{id}_pong-testbranch", new PingPongServiceConstructProps()
            {
                ServiceContainerImage = pongImage,
                EnvoyImage = envoyImage,
                TaskExecutionRole = taskExecRole,
                ServiceName = "pong",
                Branch = "testbranch",
                Mesh = props.Mesh,
                CloudmapNamespace = props.CloudmapNamespace,
                VirturalNodeName = $"pong-service-testbranch-node",
                Cluster = props.Cluster,
                Vpc = props.Vpc,
                Backends = new VirtualService[] { },
                VirtualRouter = props.PongVirtualRouter,
                RoutePriority = 1
            });
        }
    }

    public class PingPongServicesProps : StackProps
    {
        public Mesh Mesh { get; set; }
        public PrivateDnsNamespace CloudmapNamespace { get; set; }
        public Cluster Cluster { get; set; }
        public IVpc Vpc { get; set; }
        public VirtualService PingVirtualService { get; set; }
        public VirtualService PongVirtualService { get; set; }
        public VirtualRouter PingVirtualRouter { get; set; }
        public VirtualRouter PongVirtualRouter { get; set; }

    }
}