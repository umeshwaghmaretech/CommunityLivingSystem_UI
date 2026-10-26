using Mesa.WhatsApp.Domain.Common;

namespace Mesa.WhatsApp.Domain.Entities;

public class BlockedNumber : BaseEntity
{
    public string PhoneE164 { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? AddedByUserId { get; set; }
}
