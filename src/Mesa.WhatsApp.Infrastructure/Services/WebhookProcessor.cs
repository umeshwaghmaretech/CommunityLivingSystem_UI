using System.Text.Json;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Application.Webhooks;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class WebhookProcessor : IWebhookProcessor
{
    private readonly IAppDbContext _db;
    private readonly IWhatsAppCloudClient _whatsApp;
    private readonly IDemoBookingService _demos;
    private readonly IAuditService _audit;
    private readonly CampaignEngineOptions _options;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        IAppDbContext db,
        IWhatsAppCloudClient whatsApp,
        IDemoBookingService demos,
        IAuditService audit,
        IOptions<CampaignEngineOptions> options,
        ILogger<WebhookProcessor> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _demos = demos;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(string rawJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("entry", out var entries))
            return;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes))
                continue;

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value))
                    continue;

                if (value.TryGetProperty("statuses", out var statuses))
                {
                    foreach (var status in statuses.EnumerateArray())
                        await HandleStatusAsync(status, ct);
                }

                if (value.TryGetProperty("messages", out var messages))
                {
                    foreach (var message in messages.EnumerateArray())
                        await HandleInboundAsync(message, ct);
                }
            }
        }
    }

    private async Task HandleStatusAsync(JsonElement status, CancellationToken ct)
    {
        var wamid = status.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var statusValue = status.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(wamid) || string.IsNullOrWhiteSpace(statusValue))
            return;

        var msg = await _db.ConversationMessages
            .FirstOrDefaultAsync(m => m.WhatsAppMessageId == wamid, ct);
        var recipient = await _db.CampaignRecipients
            .Include(r => r.Campaign)
            .FirstOrDefaultAsync(r => r.WhatsAppMessageId == wamid, ct);

        var mapped = statusValue.ToLowerInvariant() switch
        {
            "sent" => MessageStatus.Sent,
            "delivered" => MessageStatus.Delivered,
            "read" => MessageStatus.Read,
            "failed" => MessageStatus.Failed,
            _ => (MessageStatus?)null
        };

        if (mapped is null)
            return;

        if (msg is not null)
        {
            msg.Status = mapped.Value;
            if (mapped == MessageStatus.Delivered) msg.DeliveredAtUtc = DateTime.UtcNow;
            if (mapped == MessageStatus.Read) msg.ReadAtUtc = DateTime.UtcNow;
            if (mapped == MessageStatus.Failed)
            {
                if (status.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                {
                    var err = errors[0];
                    msg.ErrorCode = err.TryGetProperty("code", out var c) ? c.ToString() : null;
                    msg.ErrorMessage = err.TryGetProperty("title", out var t) ? t.GetString() : null;
                }
            }
        }

        if (recipient is not null)
        {
            recipient.Status = mapped.Value;
            if (mapped == MessageStatus.Delivered)
            {
                recipient.DeliveredAtUtc = DateTime.UtcNow;
                recipient.Campaign.DeliveredCount++;
            }
            if (mapped == MessageStatus.Read)
            {
                recipient.ReadAtUtc = DateTime.UtcNow;
                recipient.Campaign.ReadCount++;
            }
            if (mapped == MessageStatus.Failed)
            {
                recipient.FailedAtUtc = DateTime.UtcNow;
                recipient.Campaign.FailedCount++;
                if (status.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                {
                    var err = errors[0];
                    var code = err.TryGetProperty("code", out var c) ? c.ToString() : null;
                    recipient.ErrorCode = code;
                    recipient.ErrorMessage = err.TryGetProperty("title", out var t) ? t.GetString() : null;
                    if (int.TryParse(code, out var codeInt) &&
                        _options.SpamOrRateLimitErrorCodes.Contains(codeInt) &&
                        recipient.Campaign.Status == CampaignStatus.Running)
                    {
                        recipient.Campaign.Status = CampaignStatus.AutoPaused;
                        recipient.Campaign.PausedAtUtc = DateTime.UtcNow;
                        recipient.Campaign.PauseReason = $"Webhook error {code}";
                        await _audit.LogAsync(
                            AuditAction.CampaignAutoPause,
                            recipient.Campaign.CreatedByUserId,
                            "Campaign",
                            recipient.Campaign.Id.ToString(),
                            recipient.Campaign.PauseReason,
                            ct: ct);
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleInboundAsync(JsonElement message, CancellationToken ct)
    {
        var fromRaw = message.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;
        var wamid = message.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var type = message.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : "text";

        if (!PhoneNumber.TryCreate(fromRaw is null ? null : "+" + fromRaw.TrimStart('+'), out var phone, out _) || phone is null)
        {
            _logger.LogWarning("Inbound message with invalid phone");
            return;
        }

        if (!string.IsNullOrWhiteSpace(wamid) &&
            await _db.ConversationMessages.AnyAsync(m => m.WhatsAppMessageId == wamid, ct))
        {
            return; // idempotent
        }

        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.PhoneE164 == phone.E164, ct);
        if (lead is null)
        {
            lead = new Lead
            {
                BusinessName = phone.E164,
                ContactName = "Unknown",
                PhoneE164 = phone.E164,
                Segment = BusinessSegment.Restaurant,
                Source = "inbound",
                Status = LeadStatus.Replied
            };
            _db.Leads.Add(lead);
            await _db.SaveChangesAsync(ct);
        }

        string? body = null;
        var msgType = MessageType.Text;
        string? interactivePayload = null;
        string? buttonOrListId = null;

        switch (type)
        {
            case "text":
                body = message.GetProperty("text").GetProperty("body").GetString();
                msgType = MessageType.Text;
                break;
            case "button":
                body = message.GetProperty("button").GetProperty("text").GetString();
                buttonOrListId = message.GetProperty("button").TryGetProperty("payload", out var bp)
                    ? bp.GetString()
                    : body;
                msgType = MessageType.ButtonReply;
                interactivePayload = message.GetProperty("button").GetRawText();
                break;
            case "interactive":
                var interactive = message.GetProperty("interactive");
                var interactiveType = interactive.GetProperty("type").GetString();
                if (interactiveType == "button_reply")
                {
                    var br = interactive.GetProperty("button_reply");
                    body = br.GetProperty("title").GetString();
                    buttonOrListId = br.GetProperty("id").GetString();
                    msgType = MessageType.ButtonReply;
                }
                else if (interactiveType == "list_reply")
                {
                    var lr = interactive.GetProperty("list_reply");
                    body = lr.GetProperty("title").GetString();
                    buttonOrListId = lr.GetProperty("id").GetString();
                    msgType = MessageType.ListReply;
                }
                interactivePayload = interactive.GetRawText();
                break;
        }

        lead.LastInboundAtUtc = DateTime.UtcNow;
        lead.UpdatedAtUtc = DateTime.UtcNow;

        _db.ConversationMessages.Add(new ConversationMessage
        {
            LeadId = lead.Id,
            Direction = MessageDirection.Inbound,
            Type = msgType,
            Status = MessageStatus.Delivered,
            Body = body,
            WhatsAppMessageId = wamid,
            InteractivePayloadJson = interactivePayload,
            DeliveredAtUtc = DateTime.UtcNow
        });

        // Stop follow-ups on any reply
        var openFollowUps = await _db.CampaignRecipients
            .Where(r => r.LeadId == lead.Id && r.NextFollowUpAtUtc != null)
            .ToListAsync(ct);
        foreach (var fu in openFollowUps)
            fu.NextFollowUpAtUtc = null;

        var campaigns = await _db.CampaignRecipients
            .Include(r => r.Campaign)
            .Where(r => r.LeadId == lead.Id)
            .Select(r => r.Campaign)
            .Distinct()
            .ToListAsync(ct);
        foreach (var c in campaigns)
            c.RepliedCount++;

        if (OptOutKeywords.IsOptOut(body))
        {
            await HandleOptOutAsync(lead, ct);
        }
        else if (buttonOrListId is not null && buttonOrListId.StartsWith("demo_slot_", StringComparison.OrdinalIgnoreCase))
        {
            await HandleDemoSlotSelectionAsync(lead, buttonOrListId, ct);
        }
        else if (InterestKeywords.IsInterest(body) ||
                 string.Equals(buttonOrListId, InterestKeywords.BookDemoButtonId, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(body, "Book a demo", StringComparison.OrdinalIgnoreCase))
        {
            await HandleInterestAsync(lead, ct);
        }
        else if (lead.Status is not LeadStatus.Hot and not LeadStatus.DemoRequested and not LeadStatus.DemoBooked)
        {
            lead.Status = LeadStatus.Replied;
            lead.NeedsHumanHandover = true;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleOptOutAsync(Lead lead, CancellationToken ct)
    {
        lead.IsOptedOut = true;
        lead.OptedOutAtUtc = DateTime.UtcNow;
        lead.Status = LeadStatus.OptedOut;
        lead.NeedsHumanHandover = false;

        if (!await _db.DoNotContactEntries.AnyAsync(x => x.PhoneE164 == lead.PhoneE164, ct))
        {
            _db.DoNotContactEntries.Add(new DoNotContactEntry
            {
                PhoneE164 = lead.PhoneE164,
                Reason = "Keyword opt-out",
                Source = "webhook"
            });
        }

        var campaigns = await _db.CampaignRecipients
            .Include(r => r.Campaign)
            .Where(r => r.LeadId == lead.Id)
            .Select(r => r.Campaign)
            .Distinct()
            .ToListAsync(ct);
        foreach (var c in campaigns)
            c.OptedOutCount++;

        await _whatsApp.SendTextAsync(lead.PhoneE164, _options.OptOutConfirmation, ct);

        _db.ConversationMessages.Add(new ConversationMessage
        {
            LeadId = lead.Id,
            Direction = MessageDirection.Outbound,
            Type = MessageType.Text,
            Status = MessageStatus.Sent,
            Body = _options.OptOutConfirmation,
            SentAtUtc = DateTime.UtcNow
        });

        await _audit.LogAsync(
            AuditAction.LeadOptOut,
            null,
            "Lead",
            lead.Id.ToString(),
            $"Phone={PhoneNumber.Mask(lead.PhoneE164)}",
            ct: ct);
    }

    private async Task HandleInterestAsync(Lead lead, CancellationToken ct)
    {
        lead.Status = LeadStatus.Hot;
        lead.NeedsHumanHandover = true;

        var campaigns = await _db.CampaignRecipients
            .Include(r => r.Campaign)
            .Where(r => r.LeadId == lead.Id)
            .Select(r => r.Campaign)
            .Distinct()
            .ToListAsync(ct);
        foreach (var c in campaigns)
            c.HotLeadCount++;

        var slots = _demos.GetAvailableSlots(DateTime.UtcNow);
        var rows = slots.Take(10).Select(s => new WhatsAppListRow(s.Id, s.DisplayLabel, "15:00–18:00 IST")).ToList();

        var body = $"Dear {lead.BusinessName} team,\n\nGreat! Please pick a demo slot (any day 15:00–18:00 IST).\n\n{_options.QrNfcLine}\n\n{_options.ClosingLine}\n{_options.CompanySignOff}";

        var result = await _whatsApp.SendInteractiveListAsync(
            new WhatsAppInteractiveListRequest(
                lead.PhoneE164,
                body,
                "Pick a slot",
                "Demo slots",
                rows),
            ct);

        _db.ConversationMessages.Add(new ConversationMessage
        {
            LeadId = lead.Id,
            Direction = MessageDirection.Outbound,
            Type = MessageType.InteractiveList,
            Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed,
            Body = body,
            WhatsAppMessageId = result.MessageId,
            SentAtUtc = DateTime.UtcNow,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage
        });
    }

    private async Task HandleDemoSlotSelectionAsync(Lead lead, string slotId, CancellationToken ct)
    {
        // demo_slot_yyyyMMdd_HHmm
        var parts = slotId.Replace("demo_slot_", "", StringComparison.OrdinalIgnoreCase).Split('_');
        if (parts.Length < 2 ||
            !DateTime.TryParseExact(parts[0] + parts[1], "yyyyMMddHHmm", null,
                System.Globalization.DateTimeStyles.None, out var slotStart))
        {
            lead.NeedsHumanHandover = true;
            return;
        }

        var booking = await _demos.CreatePendingAsync(lead.Id, slotStart, ct);
        lead.Status = LeadStatus.DemoRequested;

        var campaigns = await _db.CampaignRecipients
            .Include(r => r.Campaign)
            .Where(r => r.LeadId == lead.Id)
            .Select(r => r.Campaign)
            .Distinct()
            .ToListAsync(ct);
        foreach (var c in campaigns)
            c.DemoRequestedCount++;

        var confirm =
            $"Dear {lead.BusinessName} team,\n\nThanks! Your demo request for {booking.SlotStartIst:ddd, dd MMM yyyy HH:mm} IST is pending approval. Our team will confirm shortly.\n\n{_options.ClosingLine}\n{_options.CompanySignOff}";

        await _whatsApp.SendTextAsync(lead.PhoneE164, confirm, ct);
        _db.ConversationMessages.Add(new ConversationMessage
        {
            LeadId = lead.Id,
            Direction = MessageDirection.Outbound,
            Type = MessageType.Text,
            Status = MessageStatus.Sent,
            Body = confirm,
            SentAtUtc = DateTime.UtcNow
        });
    }
}
