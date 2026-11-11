using System;
using TiaMcp.Contracts;

namespace TiaMcp.Contracts.Test;

[TestClass]
public sealed class ResponseFormatSelectorTests
{
    [TestMethod]
    public void AutoSelectsCsvForEligibleSummaryTable()
    {
        var selected = ResponseFormatSelector.Select(
            ResponseFormat.Auto,
            PresentationDetailLevel.Summary,
            isTabularEligible: true);

        Assert.AreEqual(ResponseFormat.Csv, selected);
    }

    [TestMethod]
    public void AutoSelectsToonForEligibleStandardTable()
    {
        var selected = ResponseFormatSelector.Select(
            ResponseFormat.Auto,
            PresentationDetailLevel.Standard,
            isTabularEligible: true);

        Assert.AreEqual(ResponseFormat.Toon, selected);
    }

    [DataTestMethod]
    [DataRow(PresentationDetailLevel.Summary)]
    [DataRow(PresentationDetailLevel.Standard)]
    public void AutoFallsBackToJsonForIneligibleTable(
        PresentationDetailLevel detailLevel)
    {
        var selected = ResponseFormatSelector.Select(
            ResponseFormat.Auto,
            detailLevel,
            isTabularEligible: false);

        Assert.AreEqual(ResponseFormat.Json, selected);
    }

    [TestMethod]
    public void AutoSelectsJsonForFullDetail()
    {
        var selected = ResponseFormatSelector.Select(
            ResponseFormat.Auto,
            PresentationDetailLevel.Full,
            isTabularEligible: true);

        Assert.AreEqual(ResponseFormat.Json, selected);
    }

    [DataTestMethod]
    [DataRow(ResponseFormat.Toon)]
    [DataRow(ResponseFormat.Csv)]
    public void ExplicitTabularFormatThrowsForIneligibleResponse(
        ResponseFormat requested)
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => ResponseFormatSelector.Select(
                requested,
                PresentationDetailLevel.Standard,
                isTabularEligible: false));

        StringAssert.Contains(exception.Message, "Use Auto or Json");
    }

    [DataTestMethod]
    [DataRow(ResponseFormat.Toon)]
    [DataRow(ResponseFormat.Csv)]
    public void ExplicitTabularFormatThrowsForFullDetail(
        ResponseFormat requested)
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => ResponseFormatSelector.Select(
                requested,
                PresentationDetailLevel.Full,
                isTabularEligible: true));

        StringAssert.Contains(exception.Message, "Summary or Standard");
    }

    [DataTestMethod]
    [DataRow(ResponseFormat.Toon)]
    [DataRow(ResponseFormat.Csv)]
    public void ExplicitEligibleTabularFormatIsHonoured(ResponseFormat requested)
    {
        var selected = ResponseFormatSelector.Select(
            requested,
            PresentationDetailLevel.Standard,
            isTabularEligible: true);

        Assert.AreEqual(requested, selected);
    }

    [TestMethod]
    public void ExplicitJsonIsAlwaysHonoured()
    {
        var selected = ResponseFormatSelector.Select(
            ResponseFormat.Json,
            PresentationDetailLevel.Full,
            isTabularEligible: false);

        Assert.AreEqual(ResponseFormat.Json, selected);
    }

    [TestMethod]
    public void InvalidFormatThrows()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => ResponseFormatSelector.Select(
                (ResponseFormat)99,
                PresentationDetailLevel.Summary,
                isTabularEligible: true));
    }

    [TestMethod]
    public void InvalidDetailLevelThrows()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => ResponseFormatSelector.Select(
                ResponseFormat.Auto,
                (PresentationDetailLevel)99,
                isTabularEligible: true));
    }
}
