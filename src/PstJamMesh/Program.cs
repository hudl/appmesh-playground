using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PstJamMesh
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new PstJamMeshStack(app, "PstJamMeshStack");
            app.Synth();
        }
    }
}
