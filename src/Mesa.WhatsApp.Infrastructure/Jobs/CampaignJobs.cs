using Hangfire;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mesa.WhatsApp.Infrastructure.Jobs;

public sealed class CampaignJobs
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignJobs> _logger;

    public CampaignJobs(IServiceScopeFactory scopeFactory, ILogger<CampaignJobs> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task TickRunningCampaignsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<ICampaignEngine>();
        var background = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();

        var campaigns = await db.Campaigns
            .Where(c => c.Status == CampaignStatus.Running)
            .ToListAsync();

        foreach (var campaign in campaigns)
        {
            if (!engine.IsWithinSendWindow(campaign, DateTime.UtcNow))
                continue;

            var next = await db.CampaignRecipients
                .Where(r => r.CampaignId == campaign.Id && r.Status == MessageStatus.Queued)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.CreatedAtUtc)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (next == Guid.Empty)
            {
                var remaining = await db.CampaignRecipients.CountAsync(r =>
                    r.CampaignId == campaign.Id &&
                    (r.Status == MessageStatus.Sending || r.NextFollowUpAtUtc != null));
                if (remaining == 0)
                {
                    campaign.Status = CampaignStatus.Completed;
                    campaign.CompletedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
                continue;
            }

            var delayMs = engine.ComputePacingDelayMs(campaign);
            background.Schedule<CampaignJobs>(
                j => j.SendRecipientAsync(next),
                TimeSpan.FromMilliseconds(delayMs));

            // Mark as Sending briefly so tick doesn't re-pick immediately
            var recipient = await db.CampaignRecipients.FirstAsync(r => r.Id == next);
            if (recipient.Status == MessageStatus.Queued)
            {
                recipient.Status = MessageStatus.Sending;
                await db.SaveChangesAsync();
            }
        }
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 120, 300])]
    public async Task SendRecipientAsync(Guid campaignRecipientId)
    {
        using var scope = _scopeFactory.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<ICampaignEngine>();
        try
        {
            await engine.ProcessRecipientSendAsync(campaignRecipientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send failed for recipient {Id}", campaignRecipientId);
            throw;
        }
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task EvaluateQualityAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<ICampaignEngine>();

        var ids = await db.Campaigns
            .Where(c => c.Status == CampaignStatus.Running)
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var id in ids)
            await engine.EvaluateAutoPauseAsync(id);
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task ProcessFollowUpsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<ICampaignEngine>();
        await engine.ProcessFollowUpsAsync();
    }
}
