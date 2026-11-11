using System;
using System.IO;

using TiaMcpServer.Security;

namespace TiaMcpServer.Test
{
    [TestClass]
    public class AssemblyHooks
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            Settings.PrepareTestAssets();
            Directory.CreateDirectory(Settings.OutputRoot);
            OutputPathPolicy.Configure(Settings.OutputRoot);
            context.WriteLine($"Test output root: {Settings.OutputRoot}");
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // Runs once after all tests in the assembly  
            // Console.WriteLine("Assembly cleanup completed");
        }
    }
}
