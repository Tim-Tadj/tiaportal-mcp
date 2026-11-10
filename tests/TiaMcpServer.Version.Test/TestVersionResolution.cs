using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Version.Test
{
    // These tests link the production version/resolver files, without Siemens APIs.
    [TestClass]
    public class TestVersionResolution
    {
        [TestMethod]
        public void ExecutableAcceptsOnlyItsCompiledMajor()
        {
            BuildVersion.Validate(BuildVersion.TiaMajorVersion);
            foreach (var major in new[] { 19, 20, 21 })
            {
                if (major != BuildVersion.TiaMajorVersion)
                {
                    Assert.ThrowsException<ArgumentException>(() => BuildVersion.Validate(major));
                }
            }
        }

        [TestMethod]
        public void ResolverUsesTheCompiledVersionDirectory()
        {
            var directories = Engineering.GetApiSearchDirectories("test-root", BuildVersion.TiaMajorVersion);
            var expected = Path.Combine("test-root", "PublicAPI", "V" + BuildVersion.TiaMajorVersion);
#if TIA_V21
            CollectionAssert.AreEqual(new[] { Path.Combine(expected, "net48") }, directories);
#else
            CollectionAssert.AreEqual(new[] { expected, Path.Combine("test-root", "Bin", "PublicAPI") }, directories);
#endif
        }
    }
}
