using System;

namespace TiaMcp.Contracts;

public static class ResponseFormatSelector
{
    public static ResponseFormat Select(
        ResponseFormat requested,
        PresentationDetailLevel detailLevel,
        bool isTabularEligible)
    {
        ValidateRequestedFormat(requested);
        ValidateDetailLevel(detailLevel);

        if (requested == ResponseFormat.Json)
        {
            return ResponseFormat.Json;
        }

        if (requested == ResponseFormat.Csv || requested == ResponseFormat.Toon)
        {
            if (!isTabularEligible || detailLevel == PresentationDetailLevel.Full)
            {
                throw new InvalidOperationException(
                    $"Response format '{requested}' requires an eligible flat " +
                    "Summary or Standard tabular response. Use Auto or Json for " +
                    "Full-detail or non-tabular responses.");
            }

            return requested;
        }

        if (!isTabularEligible || detailLevel == PresentationDetailLevel.Full)
        {
            return ResponseFormat.Json;
        }

        return detailLevel == PresentationDetailLevel.Summary
            ? ResponseFormat.Csv
            : ResponseFormat.Toon;
    }

    private static void ValidateRequestedFormat(ResponseFormat requested)
    {
        switch (requested)
        {
            case ResponseFormat.Auto:
            case ResponseFormat.Toon:
            case ResponseFormat.Csv:
            case ResponseFormat.Json:
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(requested));
        }
    }

    private static void ValidateDetailLevel(PresentationDetailLevel detailLevel)
    {
        switch (detailLevel)
        {
            case PresentationDetailLevel.Summary:
            case PresentationDetailLevel.Standard:
            case PresentationDetailLevel.Full:
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(detailLevel));
        }
    }
}
