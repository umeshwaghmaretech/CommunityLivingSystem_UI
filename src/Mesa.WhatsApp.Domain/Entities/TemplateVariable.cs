using Mesa.WhatsApp.Domain.Common;

namespace Mesa.WhatsApp.Domain.Entities;

public class TemplateVariable : BaseEntity
{
    public Guid MessageTemplateId { get; set; }
    public MessageTemplate MessageTemplate { get; set; } = null!;
    public int Index { get; set; }
    public string Placeholder { get; set; } = string.Empty;
    public string MappedField { get; set; } = string.Empty;
    public string? ExampleValue { get; set; }
}
