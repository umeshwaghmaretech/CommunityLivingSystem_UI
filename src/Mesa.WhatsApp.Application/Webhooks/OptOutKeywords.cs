namespace Mesa.WhatsApp.Application.Webhooks;

public static class OptOutKeywords
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "STOP",
        "UNSUBSCRIBE",
        "NOT INTERESTED",
        "OPTOUT",
        "OPT OUT",
        "OPT-OUT",
        "CANCEL"
    };

    public static bool IsOptOut(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalised = text.Trim();
        if (Keywords.Contains(normalised))
            return true;

        // Also match when the whole message is essentially an opt-out phrase
        return Keywords.Any(k =>
            normalised.Equals(k, StringComparison.OrdinalIgnoreCase) ||
            normalised.StartsWith(k + " ", StringComparison.OrdinalIgnoreCase));
    }
}

public static class InterestKeywords
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "BOOK A DEMO",
        "DEMO",
        "INTERESTED",
        "YES",
        "SCHEDULE DEMO",
        "BOOK DEMO"
    };

    public const string BookDemoButtonId = "book_demo";

    public static bool IsInterest(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalised = text.Trim();
        return Keywords.Contains(normalised);
    }
}
