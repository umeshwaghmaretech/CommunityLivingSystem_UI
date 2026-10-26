namespace Mesa.WhatsApp.Application.Options;

public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string BusinessAccountId { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string GraphApiVersion { get; set; } = "v20.0";
    public string DefaultLanguage { get; set; } = "en";
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";
}
