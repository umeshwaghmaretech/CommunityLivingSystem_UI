using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class DemoBooking : BaseEntity
{
    public Guid LeadId { get; set; }
    public Lead Lead { get; set; } = null!;
    public DateTime SlotStartIst { get; set; }
    public DateTime SlotEndIst { get; set; }
    public DemoBookingStatus Status { get; set; } = DemoBookingStatus.PendingApproval;
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
}
