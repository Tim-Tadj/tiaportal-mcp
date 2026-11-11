using System.IO;
using System.Reflection;
using TiaMcpServer.Runtime;

namespace TiaMcp.Contracts.Test;

[TestClass]
public sealed class SiemensEngineeringAssemblyPolicyTests
{
    [TestMethod]
    public void FindFirstMatch_SkipsEarlierVersionBeforeExactCandidate()
    {
        var requested = Identity(
            "Siemens.Engineering.AddIn.CntxtMn",
            19,
            "37a18b206f7724a6");
        var earlierVersion = Identity(
            "Siemens.Engineering.AddIn.CntxtMn",
            18,
            "37a18b206f7724a6");
        var exactCandidate = Identity(
            "Siemens.Engineering.AddIn.CntxtMn",
            19,
            "37a18b206f7724a6");

        var selected = SiemensEngineeringAssemblyPolicy.FindFirstMatch(
            requested,
            new[] { earlierVersion, exactCandidate },
            candidate => candidate);

        Assert.AreSame(exactCandidate, selected);
    }

    [TestMethod]
    public void MatchesRequestedIdentity_AcceptsDependencySpecificToken()
    {
        var requested = Identity(
            "Siemens.Engineering.Contract",
            19,
            "37a18b206f7724a6");

        SiemensEngineeringAssemblyPolicy.DemandTrustedRequest(requested);

        Assert.IsTrue(
            SiemensEngineeringAssemblyPolicy.MatchesRequestedIdentity(
                requested,
                Identity(
                    "Siemens.Engineering.Contract",
                    19,
                    "37a18b206f7724a6")));
    }

    [TestMethod]
    public void MatchesRequestedIdentity_RejectsDifferentDependencyToken()
    {
        var requested = Identity(
            "Siemens.Engineering.Contract",
            19,
            "37a18b206f7724a6");

        Assert.IsFalse(
            SiemensEngineeringAssemblyPolicy.MatchesRequestedIdentity(
                requested,
                Identity(
                    "Siemens.Engineering.Contract",
                    19,
                    "82af32ec2e7eb4c1")));
    }

    [TestMethod]
    public void DemandTrustedRequest_RejectsUnexpectedRootToken()
    {
        Assert.ThrowsException<FileLoadException>(() =>
            SiemensEngineeringAssemblyPolicy.DemandTrustedRequest(
                Identity(
                    "Siemens.Engineering",
                    19,
                    "37a18b206f7724a6")));
    }

    private static AssemblyName Identity(
        string name,
        int majorVersion,
        string publicKeyToken)
    {
        return new AssemblyName(
            $"{name}, Version={majorVersion}.0.0.0, Culture=neutral, " +
            $"PublicKeyToken={publicKeyToken}");
    }
}
