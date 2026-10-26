using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class MessageTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public TemplateCategory Category { get; set; }
    public TemplateStatus Status { get; set; } = TemplateStatus.Pending;
    public string? BodyText { get; set; }
    public string? HeaderType { get; set; }
    public string? FooterText { get; set; }
    public string? MetaTemplateId { get; set; }
    public string? ComponentsJson { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public bool HasMediaHeader { get; set; }

    public ICollection<TemplateVariable> Variables { get; set; } = new List<TemplateVariable>();
}
