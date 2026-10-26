using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ITemplateSyncService _sync;
    private readonly IWhatsAppCloudClient _whatsApp;
    private readonly IAuditService _audit;

    public TemplatesController(
        IAppDbContext db,
        ITemplateSyncService sync,
        IWhatsAppCloudClient whatsApp,
        IAuditService audit)
    {
        _db = db;
        _sync = sync;
        _whatsApp = whatsApp;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.MessageTemplates.AsNoTracking()
            .Include(t => t.Variables)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("sync")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var count = await _sync.SyncFromMetaAsync(ct);
        return Ok(new { synced = count });
    }

    public sealed record UpsertTemplateRequest(
        string Name,
        string Language,
        TemplateCategory Category,
        bool HasMediaHeader,
        string? BodyText,
        IReadOnlyList<VariableDto>? Variables);

    public sealed record VariableDto(int Index, string Placeholder, string MappedField, string? ExampleValue);

    [HttpPost]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Upsert([FromBody] UpsertTemplateRequest request, CancellationToken ct)
    {
        var existing = await _db.MessageTemplates
            .Include(t => t.Variables)
            .FirstOrDefaultAsync(t => t.Name == request.Name && t.Language == request.Language, ct);

        if (existing is null)
        {
            existing = new MessageTemplate
            {
                Name = request.Name,
                Language = request.Language,
                Category = request.Category,
                HasMediaHeader = request.HasMediaHeader,
                BodyText = request.BodyText,
                Status = TemplateStatus.Pending
            };
            _db.MessageTemplates.Add(existing);
        }
        else
        {
            existing.Category = request.Category;
            existing.HasMediaHeader = request.HasMediaHeader;
            existing.BodyText = request.BodyText;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.Variables.Clear();
        }

        if (request.Variables is not null)
        {
            foreach (var v in request.Variables)
            {
                existing.Variables.Add(new TemplateVariable
                {
                    Index = v.Index,
                    Placeholder = v.Placeholder,
                    MappedField = v.MappedField,
                    ExampleValue = v.ExampleValue
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(existing);
    }

    [HttpPost("media/{segment}")]
    [Authorize(Policy = "ManagerOrAbove")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadSegmentMedia(
        BusinessSegment segment,
        [FromQuery] MediaKind kind,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File required" });

        var mediaRoot = Path.Combine(AppContext.BaseDirectory, "media");
        Directory.CreateDirectory(mediaRoot);
        var safeName = $"{segment}_{kind}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(mediaRoot, safeName);
        await using (var fs = System.IO.File.Create(path))
        {
            await file.CopyToAsync(fs, ct);
        }

        await using var uploadStream = System.IO.File.OpenRead(path);
        var mediaId = await _whatsApp.UploadMediaAsync(uploadStream, file.FileName, file.ContentType, ct);

        var existing = await _db.SegmentMedia.FirstOrDefaultAsync(m => m.Segment == segment && m.Kind == kind, ct);
        if (existing is null)
        {
            existing = new SegmentMedia
            {
                Segment = segment,
                Kind = kind,
                FileName = file.FileName,
                ContentType = file.ContentType,
                LocalPath = path,
                WhatsAppMediaId = mediaId,
                MediaIdCachedAtUtc = DateTime.UtcNow,
                MediaIdExpiresAtUtc = DateTime.UtcNow.AddDays(25)
            };
            _db.SegmentMedia.Add(existing);
        }
        else
        {
            existing.FileName = file.FileName;
            existing.ContentType = file.ContentType;
            existing.LocalPath = path;
            existing.WhatsAppMediaId = mediaId;
            existing.MediaIdCachedAtUtc = DateTime.UtcNow;
            existing.MediaIdExpiresAtUtc = DateTime.UtcNow.AddDays(25);
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.MediaUpload, null, "SegmentMedia", existing.Id.ToString(), $"{segment}/{kind}", ct: ct);
        return Ok(new { existing.Id, existing.WhatsAppMediaId, existing.Segment, existing.Kind });
    }
}
