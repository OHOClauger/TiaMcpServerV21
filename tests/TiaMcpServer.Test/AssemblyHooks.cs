using System;

namespace TiaMcpServer.Test
{
    [TestClass]
    public class AssemblyHooks
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            // Runs once before any tests in the assembly  
            // context.WriteLine("Assembly initialization started");
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // Runs once after all tests in the assembly  
            // Console.WriteLine("Assembly cleanup completed");
        }
    }
}
