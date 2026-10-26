using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Infrastructure.Persistence;
using Mesa.WhatsApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Mesa.WhatsApp.Tests.Campaigns;

public class AutoPauseLogicTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EvaluateAutoPause_PausesOnYellowQuality()
    {
        await using var db = CreateDb();
        var campaign = new Campaign
        {
            Name = "Test",
            Status = CampaignStatus.Running,
            MessageTemplateId = Guid.NewGuid(),
            SegmentsCsv = "Restaurant",
            CreatedByUserId = "test"
        };
        db.Campaigns.Add(campaign);
        // Need a template FK — add a stub template
        var template = new MessageTemplate
        {
            Id = campaign.MessageTemplateId,
            Name = "t",
            Language = "en",
            Status = TemplateStatus.Approved,
            Category = TemplateCategory.Marketing
        };
        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync();

        var whatsApp = new Mock<IWhatsAppCloudClient>();
        whatsApp.Setup(w => w.GetPhoneQualityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppPhoneQualityInfo(QualityRating.Yellow, "TIER_1K", "{}"));

        var audit = new Mock<IAuditService>();
        var engine = new CampaignEngine(
            db,
            whatsApp.Object,
            Mock.Of<IContentRulesValidator>(),
            audit.Object,
            Options.Create(new CampaignEngineOptions()),
            NullLogger<CampaignEngine>.Instance);

        await engine.EvaluateAutoPauseAsync(campaign.Id);

        var updated = await db.Campaigns.FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(CampaignStatus.AutoPaused, updated.Status);
        Assert.Contains("Yellow", updated.PauseReason);
        audit.Verify(a => a.LogAsync(
            AuditAction.CampaignAutoPause,
            It.IsAny<string?>(),
            "Campaign",
            campaign.Id.ToString(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAutoPause_PausesWhenOptOutRateExceedsThreshold()
    {
        await using var db = CreateDb();
        var template = new MessageTemplate
        {
            Name = "t",
            Language = "en",
            Status = TemplateStatus.Approved,
            Category = TemplateCategory.Marketing
        };
        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync();

        var campaign = new Campaign
        {
            Name = "Test",
            Status = CampaignStatus.Running,
            MessageTemplateId = template.Id,
            SegmentsCsv = "Restaurant",
            CreatedByUserId = "test",
            SentCount = 100,
            OptedOutCount = 10,
            BlockReportRateThreshold = 0.05,
            LastQualityRating = QualityRating.Green
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var whatsApp = new Mock<IWhatsAppCloudClient>();
        whatsApp.Setup(w => w.GetPhoneQualityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppPhoneQualityInfo(QualityRating.Green, "TIER_1K", "{}"));

        var engine = new CampaignEngine(
            db,
            whatsApp.Object,
            Mock.Of<IContentRulesValidator>(),
            Mock.Of<IAuditService>(),
            Options.Create(new CampaignEngineOptions()),
            NullLogger<CampaignEngine>.Instance);

        await engine.EvaluateAutoPauseAsync(campaign.Id);

        var updated = await db.Campaigns.FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(CampaignStatus.AutoPaused, updated.Status);
        Assert.Contains("rate", updated.PauseReason, StringComparison.OrdinalIgnoreCase);
    }
}
