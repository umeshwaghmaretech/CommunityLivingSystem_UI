using System.Security.Cryptography;
using System.Text;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Infrastructure.WhatsApp;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Tests.Webhooks;

public class WebhookSignatureValidatorTests
{
    private static WebhookSignatureValidator Create(string secret) =>
        new(Options.Create(new WhatsAppOptions { AppSecret = secret }));

    private static string Sign(string secret, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void IsValid_AcceptsMatchingSignature()
    {
        const string secret = "test_app_secret_value";
        const string body = "{\"object\":\"whatsapp_business_account\"}";
        var sut = Create(secret);

        Assert.True(sut.IsValid(body, Sign(secret, body)));
    }

    [Fact]
    public void IsValid_RejectsTamperedBody()
    {
        const string secret = "test_app_secret_value";
        var sut = Create(secret);
        var signature = Sign(secret, "{\"ok\":true}");

        Assert.False(sut.IsValid("{\"ok\":false}", signature));
    }

    [Fact]
    public void IsValid_RejectsMissingHeader()
    {
        var sut = Create("secret");
        Assert.False(sut.IsValid("{}", null));
        Assert.False(sut.IsValid("{}", ""));
        Assert.False(sut.IsValid("{}", "md5=abc"));
    }

    [Fact]
    public void IsValid_RejectsWrongSecret()
    {
        const string body = "{\"a\":1}";
        var sut = Create("correct_secret");
        Assert.False(sut.IsValid(body, Sign("wrong_secret", body)));
    }
}
