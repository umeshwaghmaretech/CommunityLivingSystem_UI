using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class PriorityRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Pincode { get; set; }
    public string? City { get; set; }
    public BusinessSegment? Segment { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}
