using Mesa.WhatsApp.Domain.Common;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Domain.Entities;

public class Campaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public Guid MessageTemplateId { get; set; }
    public MessageTemplate MessageTemplate { get; set; } = null!;

    /// <summary>Comma-separated BusinessSegment values.</summary>
    public string SegmentsCsv { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public string AllowedDaysCsv { get; set; } = "1,2,3,4,5,6"; // Mon–Sat (DayOfWeek: Sunday=0)
    public TimeSpan WindowStart { get; set; } = new(11, 0, 0);
    public TimeSpan WindowEnd { get; set; } = new(18, 0, 0);
    public int DailyCap { get; set; } = 200;
    public int PacingDelayMs { get; set; } = 3000;
    public int PacingJitterMs { get; set; } = 1500;
    public int FollowUpAfterDays { get; set; } = 3;
    public int MaxFollowUps { get; set; } = 2;
    public Guid? FollowUpTemplateId { get; set; }
    public MessageTemplate? FollowUpTemplate { get; set; }

    public DateTime? ScheduledStartUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? PausedAtUtc { get; set; }
    public string? PauseReason { get; set; }
    public QualityRating LastQualityRating { get; set; } = QualityRating.Unknown;
    public double BlockReportRateThreshold { get; set; } = 0.05;
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int RepliedCount { get; set; }
    public int OptedOutCount { get; set; }
    public int FailedCount { get; set; }
    public int HotLeadCount { get; set; }
    public int DemoRequestedCount { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    public ICollection<CampaignRecipient> Recipients { get; set; } = new List<CampaignRecipient>();
}
