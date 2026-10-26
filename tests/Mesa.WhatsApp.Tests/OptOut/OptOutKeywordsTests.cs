using Mesa.WhatsApp.Application.Webhooks;

namespace Mesa.WhatsApp.Tests.OptOut;

public class OptOutKeywordsTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("stop")]
    [InlineData("Unsubscribe")]
    [InlineData("not interested")]
    [InlineData("OPT-OUT")]
    [InlineData("CANCEL")]
    public void IsOptOut_DetectsKeywords(string text) =>
        Assert.True(OptOutKeywords.IsOptOut(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("please call me")]
    [InlineData("Book a demo")]
    public void IsOptOut_IgnoresOtherText(string? text) =>
        Assert.False(OptOutKeywords.IsOptOut(text));

    [Theory]
    [InlineData("DEMO")]
    [InlineData("interested")]
    [InlineData("Book a demo")]
    [InlineData("YES")]
    public void IsInterest_DetectsKeywords(string text) =>
        Assert.True(InterestKeywords.IsInterest(text));
}
