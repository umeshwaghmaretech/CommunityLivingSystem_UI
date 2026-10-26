using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class CampaignRecipient : BaseEntity
{
    public Guid CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    public Guid LeadId { get; set; }
    public Lead Lead { get; set; } = null!;

    public MessageStatus Status { get; set; } = MessageStatus.Queued;
    public int Priority { get; set; } = 100;
    public string? WhatsAppMessageId { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int FollowUpNumber { get; set; }
    public DateTime? NextFollowUpAtUtc { get; set; }
    public bool IdempotencyKeyClaimed { get; set; }

    /// <summary>Unique key: CampaignId + LeadId + FollowUpNumber — enforces one send per attempt.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}
