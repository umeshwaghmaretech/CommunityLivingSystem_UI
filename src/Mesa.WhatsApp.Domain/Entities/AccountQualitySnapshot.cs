using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class AccountQualitySnapshot : BaseEntity
{
    public QualityRating QualityRating { get; set; }
    public string? MessagingTier { get; set; }
    public int? MessagingLimit { get; set; }
    public double? BlockRate { get; set; }
    public double? ReportRate { get; set; }
    public string? RawJson { get; set; }
}
