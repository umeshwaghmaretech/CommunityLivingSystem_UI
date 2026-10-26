namespace Mesa.WhatsApp.Application.Options;

public class CampaignEngineOptions
{
    public const string SectionName = "CampaignEngine";

    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public int DefaultDailyCap { get; set; } = 200;
    public int DefaultPacingDelayMs { get; set; } = 3000;
    public int DefaultPacingJitterMs { get; set; } = 1500;
    public int MaxRetries { get; set; } = 3;
    public int[] TransientErrorCodes { get; set; } = [130429, 131000];
    public int[] SpamOrRateLimitErrorCodes { get; set; } = [131048, 131056, 130429];
    public double DefaultBlockReportRateThreshold { get; set; } = 0.05;
    public string CompanyPhone { get; set; } = "+91 8855008116";
    public string CompanySignOff { get; set; } = "Team UB Nexora Technologies | +91 8855008116";
    public string ClosingLine { get; set; } = "Let's connect!";
    public string QrNfcLine { get; set; } = "Scan the QR or tap NFC, no app needed";
    public string OptOutConfirmation { get; set; } =
        "You have been unsubscribed. You will not receive further marketing messages from MESA. Reply START if this was a mistake.";
}
