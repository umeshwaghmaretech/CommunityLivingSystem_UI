using System.Text.RegularExpressions;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Application.Validation;

public sealed partial class ContentRulesValidator : IContentRulesValidator
{
    private readonly CampaignEngineOptions _options;

    public ContentRulesValidator(IOptions<CampaignEngineOptions> options)
    {
        _options = options.Value;
    }

    public ContentValidationResult ValidateOutboundBody(string businessName, string body)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(body))
        {
            errors.Add("Message body is empty.");
            return new ContentValidationResult(false, errors);
        }

        if (PricePattern().IsMatch(body))
            errors.Add("Messages must never include prices.");

        var expectedOpen = $"Dear {businessName} team,";
        if (!body.TrimStart().StartsWith(expectedOpen, StringComparison.Ordinal))
            errors.Add($"Messages must open with \"{expectedOpen}\".");

        if (!body.Contains(_options.ClosingLine, StringComparison.Ordinal))
            errors.Add($"Messages must include closing line \"{_options.ClosingLine}\".");

        if (!body.Contains(_options.CompanySignOff, StringComparison.Ordinal))
            errors.Add($"Messages must close with \"{_options.CompanySignOff}\".");

        if (!body.Contains(_options.QrNfcLine, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Messages must include \"{_options.QrNfcLine}\".");

        // Company name may appear only once (in the sign-off).
        var companyName = "UB Nexora Technologies";
        var occurrences = CountOccurrences(body, companyName);
        if (occurrences != 1)
            errors.Add($"Company name \"{companyName}\" must appear exactly once (in the sign-off). Found {occurrences}.");

        return new ContentValidationResult(errors.Count == 0, errors);
    }

    public string BuildCompliantBody(string businessName, string middleContent)
    {
        var middle = middleContent.Trim();
        if (!middle.Contains(_options.QrNfcLine, StringComparison.OrdinalIgnoreCase))
            middle = $"{middle}\n\n{_options.QrNfcLine}";

        return $"Dear {businessName} team,\n\n{middle}\n\n{_options.ClosingLine}\n{_options.CompanySignOff}";
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    // Matches currency amounts / price-like tokens (₹, Rs., INR, $ followed by digits)
    [GeneratedRegex(@"(\u20B9|Rs\.?\s*|INR\s*|USD\s*|\$)\s*\d[\d,]*(?:\.\d+)?", RegexOptions.IgnoreCase)]
    private static partial Regex PricePattern();
}
