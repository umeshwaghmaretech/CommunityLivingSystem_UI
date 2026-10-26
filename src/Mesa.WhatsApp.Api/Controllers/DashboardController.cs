using Mesa.WhatsApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("campaigns/{id:guid}/stats")]
    public async Task<IActionResult> CampaignStats(Guid id, CancellationToken ct) =>
        Ok(await _dashboard.GetCampaignStatsAsync(id, ct));

    [HttpGet("stats/by-segment-city")]
    public async Task<IActionResult> SegmentCity([FromQuery] Guid? campaignId, CancellationToken ct) =>
        Ok(await _dashboard.GetSegmentCityStatsAsync(campaignId, ct));

    [HttpGet("campaigns/{id:guid}/export.csv")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> ExportCsv(Guid id, CancellationToken ct)
    {
        var bytes = await _dashboard.ExportCampaignReportCsvAsync(id, ct);
        return File(bytes, "text/csv", $"campaign-{id:N}.csv");
    }

    [HttpGet("campaigns/{id:guid}/export.xlsx")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> ExportXlsx(Guid id, CancellationToken ct)
    {
        var bytes = await _dashboard.ExportCampaignReportXlsxAsync(id, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"campaign-{id:N}.xlsx");
    }
}
