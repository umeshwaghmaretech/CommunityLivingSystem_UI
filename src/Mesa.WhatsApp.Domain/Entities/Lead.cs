using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class Lead : BaseEntity
{
    public string BusinessName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string PhoneE164 { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Pincode { get; set; }
    public BusinessSegment Segment { get; set; }
    public string? Source { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public bool IsOptedOut { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime? OptedOutAtUtc { get; set; }
    public DateTime? LastInboundAtUtc { get; set; }
    public DateTime? LastOutboundAtUtc { get; set; }
    public bool NeedsHumanHandover { get; set; }
    public int FollowUpCount { get; set; }
    public string? Notes { get; set; }

    public ICollection<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
    public ICollection<CampaignRecipient> CampaignRecipients { get; set; } = new List<CampaignRecipient>();
    public ICollection<DemoBooking> DemoBookings { get; set; } = new List<DemoBooking>();
}
