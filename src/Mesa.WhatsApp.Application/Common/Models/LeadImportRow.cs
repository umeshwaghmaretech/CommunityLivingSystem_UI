using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Application.Common.Models;

public class LeadImportRow
{
    public string BusinessName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Pincode { get; set; }
    public string Segment { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Status { get; set; }
}

public static class SegmentParser
{
    public static bool TryParse(string? raw, out BusinessSegment segment)
    {
        segment = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var normalised = raw.Trim().ToLowerInvariant()
            .Replace("&", "and")
            .Replace("-", " ")
            .Replace("_", " ");

        while (normalised.Contains("  ", StringComparison.Ordinal))
            normalised = normalised.Replace("  ", " ", StringComparison.Ordinal);

        segment = normalised switch
        {
            "restaurant" => BusinessSegment.Restaurant,
            "cafe" or "café" => BusinessSegment.Cafe,
            "dhaba" => BusinessSegment.Dhaba,
            "hotel and banquet" or "hotel & banquet" or "hotel" or "banquet" => BusinessSegment.HotelAndBanquet,
            "bar and lounge" or "bar & lounge" or "bar" or "lounge" => BusinessSegment.BarAndLounge,
            "takeaway and juice counter" or "takeaway & juice counter" or "takeaway" or "juice counter" => BusinessSegment.TakeawayAndJuiceCounter,
            _ => default
        };

        return segment != default || Enum.TryParse(raw.Replace(" ", "").Replace("&", "And"), true, out segment);
    }
}