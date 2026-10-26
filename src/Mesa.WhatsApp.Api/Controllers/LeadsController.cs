using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/leads")]
[Authorize(Policy = "SalesOrAbove")]
public class LeadsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ILeadImportService _import;
    private readonly IAuditService _audit;

    public LeadsController(IAppDbContext db, ILeadImportService import, IAuditService audit)
    {
        _db = db;
        _import = import;
        _audit = audit;
    }

    [HttpGet]
    [Authorize(Policy = "AnyAuthenticated")]
    public async Task<IActionResult> List(
        [FromQuery] BusinessSegment? segment,
        [FromQuery] string? city,
        [FromQuery] LeadStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Leads.AsNoTracking().AsQueryable();
        if (segment is not null) query = query.Where(l => l.Segment == segment);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(l => l.City == city);
        if (status is not null) query = query.Where(l => l.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.BusinessName,
                l.ContactName,
                Phone = PhoneNumber.Mask(l.PhoneE164),
                l.City,
                l.Pincode,
                l.Segment,
                l.Status,
                l.Source,
                l.IsOptedOut,
                l.IsBlocked,
                l.NeedsHumanHandover
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost("import")]
    [Authorize(Policy = "ManagerOrAbove")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "CSV file required" });

        await using var stream = file.OpenReadStream();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");
        var result = await _import.ImportFromCsvAsync(stream, userId, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/opt-out")]
    [Authorize(Policy = "SalesOrAbove")]
    public async Task<IActionResult> OptOut(Guid id, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return NotFound();

        lead.IsOptedOut = true;
        lead.OptedOutAtUtc = DateTime.UtcNow;
        lead.Status = LeadStatus.OptedOut;
        if (!await _db.DoNotContactEntries.AnyAsync(x => x.PhoneE164 == lead.PhoneE164, ct))
        {
            _db.DoNotContactEntries.Add(new DoNotContactEntry
            {
                PhoneE164 = lead.PhoneE164,
                Reason = "Manual opt-out",
                Source = "admin"
            });
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.LeadOptOut, User.FindFirstValue("sub"), "Lead", id.ToString(), null, ct: ct);
        return Ok();
    }

    [HttpPost("blocked")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Block([FromBody] BlockRequest request, CancellationToken ct)
    {
        if (!PhoneNumber.TryCreate(request.Phone, out var phone, out var error) || phone is null)
            return BadRequest(new { message = error });

        if (!await _db.BlockedNumbers.AnyAsync(x => x.PhoneE164 == phone.E164, ct))
        {
            _db.BlockedNumbers.Add(new BlockedNumber
            {
                PhoneE164 = phone.E164,
                Reason = request.Reason,
                AddedByUserId = User.FindFirstValue("sub")
            });
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync(AuditAction.LeadBlock, User.FindFirstValue("sub"), "BlockedNumber", phone.E164, request.Reason, ct: ct);
        return Ok();
    }

    public sealed record BlockRequest(string Phone, string? Reason);
}
