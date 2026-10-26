using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class AuditLog : BaseEntity
{
    public AuditAction Action { get; set; }
    public string? UserId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
