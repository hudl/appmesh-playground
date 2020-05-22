using System;
using Amazon.CDK;

namespace cdk
{
    public class PingPongStack : Stack
    {
        public PingPongStack(Construct scope, string id) : base(scope, id)
        {
            var environment = new Amazon.CDK.Environment()
            {
                Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
            };
            Console.Out.WriteLine($"Using AWS_REGION={environment.Region}");
            Console.Out.WriteLine($"Using AWS_ACCOUNT={environment.Account}");


            var existingVpcName = System.Environment.GetEnvironmentVariable("VPC_NAME");
            var stackBranchName = System.Environment.GetEnvironmentVariable("STACK_BRANCH_NAME");
            Console.Out.WriteLine($"Using STACK_BRANCH_NAME={stackBranchName}");

            var networkStack = new PingPongNetwork(this, $"ping-pong-network-{stackBranchName}", new PingPongNetworkProps()
            {
                UseExistingVpc = existingVpcName != null,
                ExistingVpcName = existingVpcName,
                Env = environment
            });
            var pingPongEcsBase = new PingPongECSBase(this, $"ping-pong-ecs-base-{stackBranchName}", new PingPongECSBaseProps()
            {
                Vpc = networkStack.PingPongVpc,
                Env = environment
            });
            var pingPongServices = new PingPongServices(this, $"ping-pong-services-{stackBranchName}", new PingPongServicesProps
            {
                Mesh = networkStack.AppMesh,
                CloudmapNamespace = networkStack.CloudmapNamespace,
                Cluster = pingPongEcsBase.EcsCluster,
                Vpc = networkStack.PingPongVpc,
                PingVirtualRouter = networkStack.PingRouter,
                PongVirtualRouter = networkStack.PongRouter,
                PingVirtualService = networkStack.PingService,
                PongVirtualService = networkStack.PongService,
                Env = environment
            });
        }
    }
}