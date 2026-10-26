using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IDashboardService
{
    Task<CampaignStatsDto> GetCampaignStatsAsync(Guid campaignId, CancellationToken ct = default);
    Task<IReadOnlyList<SegmentCityStatsDto>> GetSegmentCityStatsAsync(Guid? campaignId, CancellationToken ct = default);
    Task<byte[]> ExportCampaignReportCsvAsync(Guid campaignId, CancellationToken ct = default);
    Task<byte[]> ExportCampaignReportXlsxAsync(Guid campaignId, CancellationToken ct = default);
}

public sealed record CampaignStatsDto(
    Guid CampaignId,
    string Name,
    CampaignStatus Status,
    int Sent,
    int Delivered,
    int Read,
    int Replied,
    int OptedOut,
    int Failed,
    int HotLeads,
    int DemosRequested,
    int Queued,
    int TotalRecipients);

public sealed record SegmentCityStatsDto(
    string Segment,
    string City,
    int Sent,
    int Delivered,
    int Read,
    int Replied,
    int OptedOut,
    int Failed,
    int HotLeads,
    int DemosRequested);
