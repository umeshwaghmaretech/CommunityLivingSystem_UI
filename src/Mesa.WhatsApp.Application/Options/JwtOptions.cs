namespace Mesa.WhatsApp.Application.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Mesa.WhatsApp";
    public string Audience { get; set; } = "Mesa.WhatsApp.Admin";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}
