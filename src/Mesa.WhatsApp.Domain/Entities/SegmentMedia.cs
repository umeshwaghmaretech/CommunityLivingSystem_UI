using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class SegmentMedia : BaseEntity
{
    public BusinessSegment Segment { get; set; }
    public MediaKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string? WhatsAppMediaId { get; set; }
    public DateTime? MediaIdCachedAtUtc { get; set; }
    public DateTime? MediaIdExpiresAtUtc { get; set; }
}
