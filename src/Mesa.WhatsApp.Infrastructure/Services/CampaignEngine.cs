using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class CampaignEngine : ICampaignEngine
{
    private readonly IAppDbContext _db;
    private readonly IWhatsAppCloudClient _whatsApp;
    private readonly IContentRulesValidator _contentRules;
    private readonly IAuditService _audit;
    private readonly CampaignEngineOptions _options;
    private readonly ILogger<CampaignEngine> _logger;

    public CampaignEngine(
        IAppDbContext db,
        IWhatsAppCloudClient whatsApp,
        IContentRulesValidator contentRules,
        IAuditService audit,
        IOptions<CampaignEngineOptions> options,
        ILogger<CampaignEngine> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _contentRules = contentRules;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnqueueEligibleRecipientsAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await _db.Campaigns
            .Include(c => c.MessageTemplate)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");

        var segments = ParseSegments(campaign.SegmentsCsv);
        var dnc = await _db.DoNotContactEntries.AsNoTracking().Select(x => x.PhoneE164).ToListAsync(ct);
        var blocked = await _db.BlockedNumbers.AsNoTracking().Select(x => x.PhoneE164).ToListAsync(ct);
        var skip = dnc.Concat(blocked).ToHashSet(StringComparer.Ordinal);

        var already = await _db.CampaignRecipients.AsNoTracking()
            .Where(r => r.CampaignId == campaignId && r.FollowUpNumber == 0)
            .Select(r => r.LeadId)
            .ToListAsync(ct);
        var alreadySet = already.ToHashSet();

        var rules = await _db.PriorityRules.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        var leads = await _db.Leads.AsNoTracking()
            .Where(l => segments.Contains(l.Segment)
                        && !l.IsOptedOut
                        && !l.IsBlocked
                        && l.Status != LeadStatus.OptedOut
                        && l.Status != LeadStatus.Blocked)
            .ToListAsync(ct);

        var toAdd = new List<CampaignRecipient>();
        foreach (var lead in leads)
        {
            if (alreadySet.Contains(lead.Id) || skip.Contains(lead.PhoneE164))
                continue;

            var priority = ResolvePriority(lead, rules);
            var key = $"{campaignId:N}:{lead.Id:N}:0";
            toAdd.Add(new CampaignRecipient
            {
                CampaignId = campaignId,
                LeadId = lead.Id,
                Status = MessageStatus.Queued,
                Priority = priority,
                FollowUpNumber = 0,
                IdempotencyKey = key
            });
        }

        if (toAdd.Count > 0)
        {
            _db.CampaignRecipients.AddRange(toAdd);
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Enqueued {Count} recipients for campaign {CampaignId}", toAdd.Count, campaignId);
    }

    public async Task ProcessRecipientSendAsync(Guid campaignRecipientId, CancellationToken ct = default)
    {
        var recipient = await _db.CampaignRecipients
            .Include(r => r.Lead)
            .Include(r => r.Campaign)
                .ThenInclude(c => c.MessageTemplate)
                    .ThenInclude(t => t.Variables)
            .FirstOrDefaultAsync(r => r.Id == campaignRecipientId, ct);

        if (recipient is null)
            return;

        var campaign = recipient.Campaign;
        if (campaign.Status is CampaignStatus.Paused or CampaignStatus.AutoPaused
            or CampaignStatus.Cancelled or CampaignStatus.Completed)
        {
            return;
        }

        if (!IsWithinSendWindow(campaign, DateTime.UtcNow))
        {
            _logger.LogDebug("Outside send window for recipient {Id}", campaignRecipientId);
            return;
        }

        var sentToday = await _db.CampaignRecipients.CountAsync(r =>
            r.CampaignId == campaign.Id &&
            r.SentAtUtc != null &&
            r.SentAtUtc.Value.Date == DateTime.UtcNow.Date, ct);

        if (sentToday >= campaign.DailyCap)
        {
            _logger.LogInformation("Daily cap reached for campaign {CampaignId}", campaign.Id);
            return;
        }

        var lead = recipient.Lead;
        if (lead.IsOptedOut || lead.IsBlocked)
        {
            recipient.Status = MessageStatus.Skipped;
            recipient.ErrorMessage = "Lead opted out or blocked";
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (await _db.DoNotContactEntries.AnyAsync(x => x.PhoneE164 == lead.PhoneE164, ct) ||
            await _db.BlockedNumbers.AnyAsync(x => x.PhoneE164 == lead.PhoneE164, ct))
        {
            recipient.Status = MessageStatus.Skipped;
            recipient.ErrorMessage = "On DNC/blocked list";
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Idempotency: claim once
        if (recipient.IdempotencyKeyClaimed ||
            recipient.Status is MessageStatus.Sent or MessageStatus.Delivered or MessageStatus.Read)
        {
            return;
        }

        recipient.IdempotencyKeyClaimed = true;
        recipient.Status = MessageStatus.Sending;
        await _db.SaveChangesAsync(ct);

        var template = recipient.FollowUpNumber > 0 && campaign.FollowUpTemplateId is Guid fuId
            ? await _db.MessageTemplates.Include(t => t.Variables).FirstAsync(t => t.Id == fuId, ct)
            : campaign.MessageTemplate;

        if (template.Status != TemplateStatus.Approved)
        {
            recipient.Status = MessageStatus.Failed;
            recipient.ErrorMessage = "Template not approved";
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Content rules apply to free-form; for templates we still validate assembled preview body.
        var previewBody = _contentRules.BuildCompliantBody(
            lead.BusinessName,
            "Discover MESA – Operating System for Restaurants. Digitise orders, tables, and payments.");

        var validation = _contentRules.ValidateOutboundBody(lead.BusinessName, previewBody);
        if (!validation.IsValid)
        {
            recipient.Status = MessageStatus.Failed;
            recipient.ErrorMessage = string.Join("; ", validation.Errors);
            await _db.SaveChangesAsync(ct);
            _logger.LogError("Content rules failed for lead {Phone}: {Errors}",
                PhoneNumber.Mask(lead.PhoneE164), recipient.ErrorMessage);
            return;
        }

        string? mediaId = null;
        MediaKind? mediaKind = null;
        if (template.HasMediaHeader)
        {
            var media = await EnsureSegmentMediaAsync(lead.Segment, ct);
            if (media is not null)
            {
                mediaId = media.WhatsAppMediaId;
                mediaKind = media.Kind;
            }
        }

        var bodyParams = BuildTemplateParameters(template, lead);
        var result = await _whatsApp.SendTemplateAsync(
            new WhatsAppTemplateSendRequest(
                lead.PhoneE164,
                template.Name,
                template.Language,
                bodyParams,
                mediaId,
                mediaKind),
            ct);

        var message = new ConversationMessage
        {
            LeadId = lead.Id,
            CampaignId = campaign.Id,
            CampaignRecipientId = recipient.Id,
            Direction = MessageDirection.Outbound,
            Type = MessageType.Template,
            Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed,
            Body = previewBody,
            TemplateName = template.Name,
            MediaId = mediaId,
            WhatsAppMessageId = result.MessageId,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage,
            SentAtUtc = result.Success ? DateTime.UtcNow : null
        };
        _db.ConversationMessages.Add(message);

        if (result.Success)
        {
            recipient.Status = MessageStatus.Sent;
            recipient.WhatsAppMessageId = result.MessageId;
            recipient.SentAtUtc = DateTime.UtcNow;
            lead.LastOutboundAtUtc = DateTime.UtcNow;
            if (lead.Status == LeadStatus.New)
                lead.Status = LeadStatus.Contacted;
            campaign.SentCount++;

            if (campaign.FollowUpAfterDays > 0 &&
                recipient.FollowUpNumber < campaign.MaxFollowUps &&
                campaign.FollowUpTemplateId is not null)
            {
                recipient.NextFollowUpAtUtc = DateTime.UtcNow.AddDays(campaign.FollowUpAfterDays);
            }

            await _audit.LogAsync(
                AuditAction.MessageSend,
                campaign.CreatedByUserId,
                "CampaignRecipient",
                recipient.Id.ToString(),
                $"Template={template.Name}; Phone={PhoneNumber.Mask(lead.PhoneE164)}",
                ct: ct);
        }
        else
        {
            recipient.Status = MessageStatus.Failed;
            recipient.FailedAtUtc = DateTime.UtcNow;
            recipient.ErrorCode = result.ErrorCode;
            recipient.ErrorMessage = result.ErrorMessage;
            recipient.RetryCount++;
            campaign.FailedCount++;

            if (result.ShouldPauseCampaign)
            {
                await AutoPauseAsync(campaign, $"WhatsApp error {result.ErrorCode}: {result.ErrorMessage}", ct);
            }
            else if (result.IsTransient && recipient.RetryCount <= _options.MaxRetries)
            {
                // Release claim for retry
                recipient.IdempotencyKeyClaimed = false;
                recipient.Status = MessageStatus.Queued;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task EvaluateAutoPauseAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        if (campaign is null || campaign.Status != CampaignStatus.Running)
            return;

        var quality = await _whatsApp.GetPhoneQualityAsync(ct);
        if (quality is not null)
        {
            campaign.LastQualityRating = quality.QualityRating;
            _db.AccountQualitySnapshots.Add(new AccountQualitySnapshot
            {
                QualityRating = quality.QualityRating,
                MessagingTier = quality.MessagingLimitTier,
                RawJson = quality.RawJson
            });

            if (quality.QualityRating is QualityRating.Yellow or QualityRating.Red)
            {
                await AutoPauseAsync(campaign, $"Quality rating dropped to {quality.QualityRating}", ct);
                await _db.SaveChangesAsync(ct);
                return;
            }
        }

        var totalSent = Math.Max(1, campaign.SentCount);
        var blockLike = campaign.OptedOutCount;
        var rate = (double)blockLike / totalSent;
        if (rate >= campaign.BlockReportRateThreshold)
        {
            await AutoPauseAsync(campaign, $"Block/opt-out rate {rate:P1} exceeded threshold", ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ProcessFollowUpsAsync(CancellationToken ct = default)
    {
        var due = await _db.CampaignRecipients
            .Include(r => r.Lead)
            .Include(r => r.Campaign)
            .Where(r =>
                r.NextFollowUpAtUtc != null &&
                r.NextFollowUpAtUtc <= DateTime.UtcNow &&
                r.FollowUpNumber < r.Campaign.MaxFollowUps &&
                r.Campaign.Status == CampaignStatus.Running &&
                !r.Lead.IsOptedOut &&
                (r.Lead.LastInboundAtUtc == null || r.Lead.LastInboundAtUtc < r.SentAtUtc))
            .Take(100)
            .ToListAsync(ct);

        foreach (var original in due)
        {
            if (original.Lead.IsOptedOut || original.Lead.LastInboundAtUtc > original.SentAtUtc)
            {
                original.NextFollowUpAtUtc = null;
                continue;
            }

            var nextNumber = original.FollowUpNumber + 1;
            var key = $"{original.CampaignId:N}:{original.LeadId:N}:{nextNumber}";
            if (await _db.CampaignRecipients.AnyAsync(r => r.IdempotencyKey == key, ct))
            {
                original.NextFollowUpAtUtc = null;
                continue;
            }

            _db.CampaignRecipients.Add(new CampaignRecipient
            {
                CampaignId = original.CampaignId,
                LeadId = original.LeadId,
                Status = MessageStatus.Queued,
                Priority = original.Priority,
                FollowUpNumber = nextNumber,
                IdempotencyKey = key
            });
            original.Lead.FollowUpCount = nextNumber;
            original.NextFollowUpAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);
    }

    public bool IsWithinSendWindow(Campaign campaign, DateTime utcNow)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(campaign.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        var allowedDays = campaign.AllowedDaysCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToHashSet();

        if (!allowedDays.Contains((int)local.DayOfWeek))
            return false;

        var tod = local.TimeOfDay;
        return tod >= campaign.WindowStart && tod <= campaign.WindowEnd;
    }

    public int ComputePacingDelayMs(Campaign campaign, Random? random = null)
    {
        random ??= Random.Shared;
        var jitter = campaign.PacingJitterMs <= 0 ? 0 : random.Next(0, campaign.PacingJitterMs + 1);
        return Math.Max(0, campaign.PacingDelayMs + jitter);
    }

    private async Task AutoPauseAsync(Campaign campaign, string reason, CancellationToken ct)
    {
        campaign.Status = CampaignStatus.AutoPaused;
        campaign.PausedAtUtc = DateTime.UtcNow;
        campaign.PauseReason = reason;
        await _audit.LogAsync(
            AuditAction.CampaignAutoPause,
            campaign.CreatedByUserId,
            "Campaign",
            campaign.Id.ToString(),
            reason,
            ct: ct);
        _logger.LogWarning("Campaign {CampaignId} auto-paused: {Reason}", campaign.Id, reason);
    }

    private async Task<SegmentMedia?> EnsureSegmentMediaAsync(BusinessSegment segment, CancellationToken ct)
    {
        // Prefer image; fall back to document
        var media = await _db.SegmentMedia
            .Where(m => m.Segment == segment)
            .OrderBy(m => m.Kind)
            .FirstOrDefaultAsync(ct);

        if (media is null)
            return null;

        if (!string.IsNullOrWhiteSpace(media.WhatsAppMediaId) &&
            media.MediaIdExpiresAtUtc is DateTime exp &&
            exp > DateTime.UtcNow.AddHours(1))
        {
            return media;
        }

        if (!File.Exists(media.LocalPath))
        {
            _logger.LogWarning("Segment media file missing: {Path}", media.LocalPath);
            return media;
        }

        await using var stream = File.OpenRead(media.LocalPath);
        var id = await _whatsApp.UploadMediaAsync(stream, media.FileName, media.ContentType, ct);
        media.WhatsAppMediaId = id;
        media.MediaIdCachedAtUtc = DateTime.UtcNow;
        media.MediaIdExpiresAtUtc = DateTime.UtcNow.AddDays(25); // Meta media IDs ~30 days
        media.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return media;
    }

    private static List<string> BuildTemplateParameters(MessageTemplate template, Lead lead)
    {
        if (template.Variables.Count == 0)
            return [lead.BusinessName];

        return template.Variables
            .OrderBy(v => v.Index)
            .Select(v => v.MappedField.ToLowerInvariant() switch
            {
                "businessname" or "business_name" => lead.BusinessName,
                "contactname" or "contact_name" => lead.ContactName,
                "city" => lead.City ?? string.Empty,
                "pincode" => lead.Pincode ?? string.Empty,
                "segment" => lead.Segment.ToString(),
                _ => lead.BusinessName
            })
            .ToList();
    }

    private static HashSet<BusinessSegment> ParseSegments(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.Parse<BusinessSegment>(s, true))
            .ToHashSet();

    private static int ResolvePriority(Lead lead, List<PriorityRule> rules)
    {
        foreach (var rule in rules)
        {
            var match = true;
            if (!string.IsNullOrWhiteSpace(rule.Pincode) &&
                !string.Equals(rule.Pincode, lead.Pincode, StringComparison.OrdinalIgnoreCase))
                match = false;
            if (!string.IsNullOrWhiteSpace(rule.City) &&
                !string.Equals(rule.City, lead.City, StringComparison.OrdinalIgnoreCase))
                match = false;
            if (rule.Segment is BusinessSegment seg && seg != lead.Segment)
                match = false;

            if (match)
                return rule.Priority;
        }

        return 100;
    }
}
