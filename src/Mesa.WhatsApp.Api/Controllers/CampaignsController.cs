using Hangfire;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Infrastructure.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize]
public class CampaignsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICampaignEngine _engine;
    private readonly IAuditService _audit;
    private readonly IBackgroundJobClient _jobs;

    public CampaignsController(
        IAppDbContext db,
        ICampaignEngine engine,
        IAuditService audit,
        IBackgroundJobClient jobs)
    {
        _db = db;
        _engine = engine;
        _audit = audit;
        _jobs = jobs;
    }

    public sealed record CreateCampaignRequest(
        string Name,
        string? Description,
        Guid MessageTemplateId,
        IReadOnlyList<BusinessSegment> Segments,
        string? TimeZoneId,
        string? AllowedDaysCsv,
        string? WindowStart,
        string? WindowEnd,
        int? DailyCap,
        int? PacingDelayMs,
        int? PacingJitterMs,
        int? FollowUpAfterDays,
        int? MaxFollowUps,
        Guid? FollowUpTemplateId,
        double? BlockReportRateThreshold);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.Campaigns.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                c.Id, c.Name, c.Status, c.SegmentsCsv, c.SentCount, c.DeliveredCount,
                c.ReadCount, c.RepliedCount, c.OptedOutCount, c.FailedCount,
                c.HotLeadCount, c.DemoRequestedCount, c.PauseReason, c.CreatedAtUtc
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        if (request.Segments.Count == 0)
            return BadRequest(new { message = "At least one segment is required" });

        var template = await _db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == request.MessageTemplateId, ct);
        if (template is null)
            return BadRequest(new { message = "Template not found" });

        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var campaign = new Campaign
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            MessageTemplateId = request.MessageTemplateId,
            SegmentsCsv = string.Join(',', request.Segments.Select(s => s.ToString())),
            TimeZoneId = request.TimeZoneId ?? "Asia/Kolkata",
            AllowedDaysCsv = request.AllowedDaysCsv ?? "1,2,3,4,5,6",
            WindowStart = TimeSpan.TryParse(request.WindowStart, out var ws) ? ws : new TimeSpan(11, 0, 0),
            WindowEnd = TimeSpan.TryParse(request.WindowEnd, out var we) ? we : new TimeSpan(18, 0, 0),
            DailyCap = request.DailyCap ?? 200,
            PacingDelayMs = request.PacingDelayMs ?? 3000,
            PacingJitterMs = request.PacingJitterMs ?? 1500,
            FollowUpAfterDays = request.FollowUpAfterDays ?? 3,
            MaxFollowUps = Math.Clamp(request.MaxFollowUps ?? 2, 0, 2),
            FollowUpTemplateId = request.FollowUpTemplateId,
            BlockReportRateThreshold = request.BlockReportRateThreshold ?? 0.05,
            CreatedByUserId = userId,
            Status = CampaignStatus.Draft
        };

        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.CampaignCreate, userId, "Campaign", campaign.Id.ToString(), campaign.Name, ct: ct);
        return CreatedAtAction(nameof(Get), new { id = campaign.Id }, campaign);
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.Include(c => c.MessageTemplate).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return NotFound();
        if (campaign.MessageTemplate.Status != TemplateStatus.Approved)
            return BadRequest(new { message = "Template must be approved before starting" });

        await _engine.EnqueueEligibleRecipientsAsync(id, ct);
        campaign.Status = CampaignStatus.Running;
        campaign.StartedAtUtc = DateTime.UtcNow;
        campaign.PauseReason = null;
        await _db.SaveChangesAsync(ct);

        var userId = User.FindFirstValue("sub");
        await _audit.LogAsync(AuditAction.CampaignStart, userId, "Campaign", id.ToString(), null, ct: ct);
        _jobs.Enqueue<CampaignJobs>(j => j.TickRunningCampaignsAsync());
        return Ok(campaign);
    }

    [HttpPost("{id:guid}/pause")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Pause(Guid id, [FromBody] PauseRequest? request, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return NotFound();
        campaign.Status = CampaignStatus.Paused;
        campaign.PausedAtUtc = DateTime.UtcNow;
        campaign.PauseReason = request?.Reason ?? "Manual pause";
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.CampaignPause, User.FindFirstValue("sub"), "Campaign", id.ToString(), campaign.PauseReason, ct: ct);
        return Ok(campaign);
    }

    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return NotFound();
        campaign.Status = CampaignStatus.Running;
        campaign.PauseReason = null;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.CampaignResume, User.FindFirstValue("sub"), "Campaign", id.ToString(), null, ct: ct);
        return Ok(campaign);
    }

    public sealed record PauseRequest(string? Reason);
}
