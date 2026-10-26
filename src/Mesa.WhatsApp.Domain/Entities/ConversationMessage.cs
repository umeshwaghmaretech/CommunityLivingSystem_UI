using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class ConversationMessage : BaseEntity
{
    public Guid LeadId { get; set; }
    public Lead Lead { get; set; } = null!;
    public Guid? CampaignId { get; set; }
    public Guid? CampaignRecipientId { get; set; }

    public MessageDirection Direction { get; set; }
    public MessageType Type { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.Queued;
    public string? Body { get; set; }
    public string? WhatsAppMessageId { get; set; }
    public string? TemplateName { get; set; }
    public string? MediaId { get; set; }
    public string? InteractivePayloadJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SentByUserId { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
