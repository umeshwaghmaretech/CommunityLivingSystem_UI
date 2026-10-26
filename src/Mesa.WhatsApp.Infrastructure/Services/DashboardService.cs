using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;

    public DashboardService(IAppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<CampaignStatsDto> GetCampaignStatsAsync(Guid campaignId, CancellationToken ct = default)
    {
        var c = await _db.Campaigns.AsNoTracking().FirstAsync(x => x.Id == campaignId, ct);
        var queued = await _db.CampaignRecipients.CountAsync(r => r.CampaignId == campaignId && r.Status == MessageStatus.Queued, ct);
        var total = await _db.CampaignRecipients.CountAsync(r => r.CampaignId == campaignId, ct);

        return new CampaignStatsDto(
            c.Id, c.Name, c.Status,
            c.SentCount, c.DeliveredCount, c.ReadCount, c.RepliedCount,
            c.OptedOutCount, c.FailedCount, c.HotLeadCount, c.DemoRequestedCount,
            queued, total);
    }

    public async Task<IReadOnlyList<SegmentCityStatsDto>> GetSegmentCityStatsAsync(Guid? campaignId, CancellationToken ct = default)
    {
        var query = _db.CampaignRecipients.AsNoTracking()
            .Include(r => r.Lead)
            .AsQueryable();

        if (campaignId is Guid id)
            query = query.Where(r => r.CampaignId == id);

        var rows = await query.ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.Lead.Segment, City = r.Lead.City ?? "Unknown" })
            .Select(g => new SegmentCityStatsDto(
                g.Key.Segment.ToString(),
                g.Key.City,
                g.Count(x => x.Status is MessageStatus.Sent or MessageStatus.Delivered or MessageStatus.Read),
                g.Count(x => x.Status is MessageStatus.Delivered or MessageStatus.Read),
                g.Count(x => x.Status == MessageStatus.Read),
                g.Count(x => x.Lead.Status is LeadStatus.Replied or LeadStatus.Hot or LeadStatus.DemoRequested or LeadStatus.DemoBooked),
                g.Count(x => x.Lead.IsOptedOut),
                g.Count(x => x.Status == MessageStatus.Failed),
                g.Count(x => x.Lead.Status == LeadStatus.Hot),
                g.Count(x => x.Lead.Status is LeadStatus.DemoRequested or LeadStatus.DemoBooked)))
            .OrderBy(x => x.Segment)
            .ThenBy(x => x.City)
            .ToList();
    }

    public async Task<byte[]> ExportCampaignReportCsvAsync(Guid campaignId, CancellationToken ct = default)
    {
        var rows = await BuildExportRowsAsync(campaignId, ct);
        await using var ms = new MemoryStream();
        await using (var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(rows);
        }

        await _audit.LogAsync(AuditAction.ReportExport, null, "Campaign", campaignId.ToString(), "CSV", ct: ct);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportCampaignReportXlsxAsync(Guid campaignId, CancellationToken ct = default)
    {
        var rows = await BuildExportRowsAsync(campaignId, ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Campaign");
        sheet.Cell(1, 1).InsertTable(rows);
        await using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        await _audit.LogAsync(AuditAction.ReportExport, null, "Campaign", campaignId.ToString(), "XLSX", ct: ct);
        return ms.ToArray();
    }

    private async Task<List<ExportRow>> BuildExportRowsAsync(Guid campaignId, CancellationToken ct)
    {
        var recipients = await _db.CampaignRecipients.AsNoTracking()
            .Include(r => r.Lead)
            .Where(r => r.CampaignId == campaignId)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        return recipients.Select(r => new ExportRow
        {
            BusinessName = r.Lead.BusinessName,
            ContactName = r.Lead.ContactName,
            PhoneMasked = PhoneNumber.Mask(r.Lead.PhoneE164),
            City = r.Lead.City,
            Pincode = r.Lead.Pincode,
            Segment = r.Lead.Segment.ToString(),
            LeadStatus = r.Lead.Status.ToString(),
            MessageStatus = r.Status.ToString(),
            FollowUpNumber = r.FollowUpNumber,
            SentAtUtc = r.SentAtUtc,
            DeliveredAtUtc = r.DeliveredAtUtc,
            ReadAtUtc = r.ReadAtUtc,
            ErrorCode = r.ErrorCode,
            ErrorMessage = r.ErrorMessage
        }).ToList();
    }

    private sealed class ExportRow
    {
        public string BusinessName { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string PhoneMasked { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public string Segment { get; set; } = string.Empty;
        public string LeadStatus { get; set; } = string.Empty;
        public string MessageStatus { get; set; } = string.Empty;
        public int FollowUpNumber { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public DateTime? DeliveredAtUtc { get; set; }
        public DateTime? ReadAtUtc { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
