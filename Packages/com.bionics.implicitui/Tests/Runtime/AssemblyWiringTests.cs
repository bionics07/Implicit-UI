using System.Reflection;
using NUnit.Framework;

namespace ImplicitUI.Tests
{
    // Guards the package layout: fails when an asmdef is renamed or stops compiling.
    public class AssemblyWiringTests
    {
        [TestCase("ImplicitUI.Runtime")]
        public void PackageAssemblyLoads(string assemblyName)
        {
            Assert.DoesNotThrow(() => Assembly.Load(assemblyName));
        }
    }
}
