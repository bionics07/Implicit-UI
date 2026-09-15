using System.Reflection;
using NUnit.Framework;

namespace ImplicitUI.Tests.EditorTests
{
    // Guards the package layout: fails when an asmdef is renamed or stops compiling.
    public class AssemblyWiringTests
    {
        [TestCase("ImplicitUI.Runtime")]
        [TestCase("ImplicitUI.Editor")]
        public void PackageAssemblyLoads(string assemblyName)
        {
            Assert.DoesNotThrow(() => Assembly.Load(assemblyName));
        }
    }
}
