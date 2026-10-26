using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Mesa.WhatsApp.Tests.Campaigns;

public class CampaignEnginePacingTests
{
    private static CampaignEngine CreateEngine()
    {
        return new CampaignEngine(
            Mock.Of<IAppDbContext>(),
            Mock.Of<IWhatsAppCloudClient>(),
            Mock.Of<IContentRulesValidator>(),
            Mock.Of<IAuditService>(),
            Options.Create(new CampaignEngineOptions()),
            NullLogger<CampaignEngine>.Instance);
    }

    [Fact]
    public void ComputePacingDelayMs_IncludesBaseAndJitter()
    {
        var engine = CreateEngine();
        var campaign = new Campaign { PacingDelayMs = 3000, PacingJitterMs = 1000 };
        var random = new Random(42);

        var delays = Enumerable.Range(0, 20).Select(_ => engine.ComputePacingDelayMs(campaign, random)).ToList();

        Assert.All(delays, d => Assert.InRange(d, 3000, 4000));
        Assert.Contains(delays, d => d > 3000);
    }

    [Fact]
    public void IsWithinSendWindow_RespectsIstWeekdayHours()
    {
        var engine = CreateEngine();
        var campaign = new Campaign
        {
            TimeZoneId = "Asia/Kolkata",
            AllowedDaysCsv = "1,2,3,4,5,6", // Mon–Sat
            WindowStart = new TimeSpan(11, 0, 0),
            WindowEnd = new TimeSpan(18, 0, 0)
        };

        // 2026-10-26 is a Monday. 06:30 UTC = 12:00 IST → inside
        var inside = new DateTime(2026, 10, 26, 6, 30, 0, DateTimeKind.Utc);
        Assert.True(engine.IsWithinSendWindow(campaign, inside));

        // 03:00 UTC = 08:30 IST → outside
        var before = new DateTime(2026, 10, 26, 3, 0, 0, DateTimeKind.Utc);
        Assert.False(engine.IsWithinSendWindow(campaign, before));

        // Sunday 2026-10-25 07:00 UTC = 12:30 IST → day not allowed
        var sunday = new DateTime(2026, 10, 25, 7, 0, 0, DateTimeKind.Utc);
        Assert.False(engine.IsWithinSendWindow(campaign, sunday));
    }
}
