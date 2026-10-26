using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class InboxService : IInboxService
{
    private readonly IAppDbContext _db;
    private readonly IWhatsAppCloudClient _whatsApp;
    private readonly IAuditService _audit;

    public InboxService(
        IAppDbContext db,
        IWhatsAppCloudClient whatsApp,
        IAuditService audit)
    {
        _db = db;
        _whatsApp = whatsApp;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsNeedingHumanAsync(CancellationToken ct = default)
    {
        var leads = await _db.Leads.AsNoTracking()
            .Where(l => l.NeedsHumanHandover && !l.IsOptedOut)
            .OrderByDescending(l => l.LastInboundAtUtc)
            .Take(100)
            .ToListAsync(ct);

        var result = new List<ConversationSummaryDto>();
        foreach (var lead in leads)
        {
            var last = await _db.ConversationMessages.AsNoTracking()
                .Where(m => m.LeadId == lead.Id)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Select(m => m.Body)
                .FirstOrDefaultAsync(ct);

            result.Add(new ConversationSummaryDto(
                lead.Id,
                lead.BusinessName,
                PhoneNumber.Mask(lead.PhoneE164),
                lead.Status,
                lead.NeedsHumanHandover,
                lead.LastInboundAtUtc,
                last is null ? null : (last.Length > 80 ? last[..80] : last)));
        }

        return result;
    }

    public async Task<IReadOnlyList<MessageDto>> GetConversationAsync(Guid leadId, CancellationToken ct = default)
    {
        return await _db.ConversationMessages.AsNoTracking()
            .Where(m => m.LeadId == leadId)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new MessageDto(
                m.Id, m.Direction, m.Type, m.Status, m.Body, m.CreatedAtUtc, m.SentByUserId))
            .ToListAsync(ct);
    }

    public async Task<MessageDto> SendManualReplyAsync(Guid leadId, string body, string userId, CancellationToken ct = default)
    {
        var lead = await _db.Leads.FirstAsync(l => l.Id == leadId, ct);

        if (lead.IsOptedOut)
            throw new InvalidOperationException("Lead has opted out.");

        // 24-hour customer service window
        if (lead.LastInboundAtUtc is null ||
            lead.LastInboundAtUtc < DateTime.UtcNow.AddHours(-24))
        {
            throw new InvalidOperationException(
                "Outside the 24-hour customer service window. Use an approved template instead.");
        }

        // Free-form replies should still avoid prices; opening/sign-off enforced for marketing-style replies
        if (body.Contains('₹') || body.Contains("Rs.", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(body, @"(\u20B9|Rs\.?\s*|INR\s*|\$)\s*\d",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            throw new InvalidOperationException("Messages must never include prices.");
        }

        var result = await _whatsApp.SendTextAsync(lead.PhoneE164, body, ct);
        if (!result.Success)
            throw new InvalidOperationException($"WhatsApp send failed: {result.ErrorCode} {result.ErrorMessage}");

        var msg = new ConversationMessage
        {
            LeadId = leadId,
            Direction = MessageDirection.Outbound,
            Type = MessageType.Text,
            Status = MessageStatus.Sent,
            Body = body,
            WhatsAppMessageId = result.MessageId,
            SentByUserId = userId,
            SentAtUtc = DateTime.UtcNow
        };
        _db.ConversationMessages.Add(msg);
        lead.LastOutboundAtUtc = DateTime.UtcNow;
        lead.NeedsHumanHandover = false;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.ManualReply,
            userId,
            "Lead",
            leadId.ToString(),
            $"Phone={PhoneNumber.Mask(lead.PhoneE164)}",
            ct: ct);

        return new MessageDto(msg.Id, msg.Direction, msg.Type, msg.Status, msg.Body, msg.CreatedAtUtc, msg.SentByUserId);
    }

    public async Task FlagForHumanAsync(Guid leadId, bool needsHuman, CancellationToken ct = default)
    {
        var lead = await _db.Leads.FirstAsync(l => l.Id == leadId, ct);
        lead.NeedsHumanHandover = needsHuman;
        lead.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
