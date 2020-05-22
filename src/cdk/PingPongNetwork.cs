using Amazon.CDK;
using Amazon.CDK.AWS.AppMesh;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ServiceDiscovery;

namespace cdk
{
    public class PingPongNetworkProps : StackProps
    {
        public string ExistingVpcName { get; set; }
        public bool UseExistingVpc { get; set; }
    }

    public class PingPongNetwork : Construct
    {
        public IVpc PingPongVpc { get; private set; }
        public PrivateDnsNamespace CloudmapNamespace { get; private set; }
        public Mesh AppMesh { get; private set; }
        public VirtualRouter PingRouter { get; private set; }
        public VirtualRouter PongRouter { get; private set; }
        public VirtualService PingService { get; private set; }
        public VirtualService PongService { get; private set; }
        public PingPongNetwork(Construct scope, string id, PingPongNetworkProps props = null) : base(scope, id)
        {
            PingPongVpc = AddVpc(id, props);
            CloudmapNamespace = CreateCloudMapNamespace(id, PingPongVpc);
            AppMesh = AddMesh(id);
            PingRouter = AddVirtualRouter(id, "ping", AppMesh);
            PongRouter = AddVirtualRouter(id, "pong", AppMesh);
            PingService = AddVirtualService(id, "ping", AppMesh, PingRouter, CloudmapNamespace);
            PongService = AddVirtualService(id, "pong", AppMesh, PongRouter, CloudmapNamespace);
        }
        internal IVpc AddVpc(string id, PingPongNetworkProps props)
        {
            if (props.UseExistingVpc)
            {
                System.Console.WriteLine($"Using VPC = {props.ExistingVpcName}");
                return Vpc.FromLookup(this, $"{id}-vpc", new VpcLookupOptions()
                {
                    VpcName = props.ExistingVpcName
                });
            }
            else
            {
                return new Vpc(this, $"{id}-vpc", new VpcProps
                {
                });
            }

        }
        internal Amazon.CDK.AWS.ServiceDiscovery.PrivateDnsNamespace CreateCloudMapNamespace(string id, IVpc vpc)
        {
            var privateNamespace = new PrivateDnsNamespace(this, $"{id}-cloudmap-private-namespace", new PrivateDnsNamespaceProps
            {
                Description = $"Cloudmap Namespace for {id}",
                Vpc = vpc,
                Name = $"{id}"
            });
            return privateNamespace;
        }

        private Mesh AddMesh(string id)
        {
            // The Mesh
            var mesh = new Mesh(this, $"{id}-mesh", new MeshProps()
            {
                EgressFilter = MeshFilterType.ALLOW_ALL,
                MeshName = $"{id}-mesh",
            });
            return mesh;
        }


        private VirtualRouter AddVirtualRouter(string id, string routerServiceName, Mesh mesh)
        {
            return mesh.AddVirtualRouter($"{id}-{routerServiceName}-service-router", new VirtualRouterProps()
            {
                Listener = new Listener()
                {
                    PortMapping = new Amazon.CDK.AWS.AppMesh.PortMapping()
                    {
                        Port = 5000,
                        Protocol = Amazon.CDK.AWS.AppMesh.Protocol.HTTP
                    }
                },
                Mesh = mesh,
                VirtualRouterName = $"{routerServiceName}-service-router"
            });
        }

        private VirtualService AddVirtualService(string id, string virtualServiceName, Mesh mesh, VirtualRouter router, PrivateDnsNamespace cloudmapNamespace)
        {
            return mesh.AddVirtualService($"{id}-{virtualServiceName}-service", new VirtualServiceProps()
            {
                Mesh = mesh,
                VirtualRouter = router,
                VirtualServiceName = $"{virtualServiceName}-service.{cloudmapNamespace.NamespaceName}"
            });
        }


    }
}