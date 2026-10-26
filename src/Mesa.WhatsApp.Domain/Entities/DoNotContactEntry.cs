using Mesa.WhatsApp.Domain.Common;

namespace Mesa.WhatsApp.Domain.Entities;

/// <summary>
/// Explicit opt-out / do-not-contact list. Always skipped by campaigns.
/// </summary>
public class DoNotContactEntry : BaseEntity
{
    public string PhoneE164 { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Source { get; set; }
}
