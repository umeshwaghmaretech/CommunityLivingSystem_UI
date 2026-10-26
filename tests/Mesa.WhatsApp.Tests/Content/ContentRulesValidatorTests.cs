using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Application.Validation;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Tests.Content;

public class ContentRulesValidatorTests
{
    private readonly ContentRulesValidator _sut = new(Options.Create(new CampaignEngineOptions()));

    [Fact]
    public void BuildCompliantBody_PassesValidation()
    {
        var body = _sut.BuildCompliantBody("Spice Garden", "MESA digitises restaurant operations.");
        var result = _sut.ValidateOutboundBody("Spice Garden", body);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_RejectsPrices()
    {
        var body = _sut.BuildCompliantBody("Spice Garden", "Plans start at ₹999 per month.");
        var result = _sut.ValidateOutboundBody("Spice Garden", body);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("prices", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsMissingOpening()
    {
        var body = "Hello team,\n\nScan the QR or tap NFC, no app needed\n\nLet's connect!\nTeam UB Nexora Technologies | +91 8855008116";
        var result = _sut.ValidateOutboundBody("Spice Garden", body);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsDuplicateCompanyName()
    {
        var body = _sut.BuildCompliantBody("Spice Garden", "Welcome to UB Nexora Technologies MESA.");
        var result = _sut.ValidateOutboundBody("Spice Garden", body);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("exactly once", StringComparison.OrdinalIgnoreCase));
    }
}
