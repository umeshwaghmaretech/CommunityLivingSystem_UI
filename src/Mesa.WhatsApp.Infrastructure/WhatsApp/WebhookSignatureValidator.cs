using System.Security.Cryptography;
using System.Text;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Infrastructure.WhatsApp;

public sealed class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly WhatsAppOptions _options;

    public WebhookSignatureValidator(IOptions<WhatsAppOptions> options)
    {
        _options = options.Value;
    }

    public bool IsValid(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.AppSecret))
            return false;

        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var providedHex = signatureHeader[prefix.Length..].Trim();
        if (providedHex.Length == 0)
            return false;

        var key = Encoding.UTF8.GetBytes(_options.AppSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
        var hash = HMACSHA256.HashData(key, bodyBytes);
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHex),
            Encoding.UTF8.GetBytes(providedHex.ToLowerInvariant()));
    }
}
