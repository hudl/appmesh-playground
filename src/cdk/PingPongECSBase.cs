
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;

namespace cdk
{
    public sealed class PingPongECSBaseProps : StackProps
    {
        public IVpc Vpc { get; set; }
    }

    public class PingPongECSBase : Construct
    {
        public Cluster EcsCluster { get; private set; }
        public PingPongECSBase(Construct scope, string id, PingPongECSBaseProps props = null) : base(scope, id)
        {
            EcsCluster = AddECSCluster(id, props.Vpc);
        }

        internal Cluster AddECSCluster(string id, IVpc vpc)
        {
            var cluster = new Cluster(this, $"{id}-ecs-cluster", new ClusterProps
            {
                ClusterName = $"{id}",
                ContainerInsights = false,
                Vpc = vpc,
            });

            return cluster;
        }
    }
}