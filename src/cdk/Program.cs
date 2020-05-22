using Amazon.CDK;
using System;

namespace cdk
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var stackBranchName = System.Environment.GetEnvironmentVariable("STACK_BRANCH_NAME");
            var app = new App();
            new PingPongStack(app, $"ping-pong-{stackBranchName}");
            app.Synth();
        }
    }
}
