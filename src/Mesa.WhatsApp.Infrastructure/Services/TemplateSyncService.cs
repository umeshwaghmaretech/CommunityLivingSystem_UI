using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mesa.WhatsApp.Infrastructure.Services;

public interface ITemplateSyncService
{
    Task<int> SyncFromMetaAsync(CancellationToken ct = default);
}

public sealed class TemplateSyncService : ITemplateSyncService
{
    private readonly IAppDbContext _db;
    private readonly IWhatsAppCloudClient _whatsApp;
    private readonly IAuditService _audit;
    private readonly ILogger<TemplateSyncService> _logger;

    public TemplateSyncService(
        IAppDbContext db,
        IWhatsAppCloudClient whatsApp,
        IAuditService audit,
        ILogger<TemplateSyncService> logger)
    {
        _db = db;
        _whatsApp = whatsApp;
        _audit = audit;
        _logger = logger;
    }

    public async Task<int> SyncFromMetaAsync(CancellationToken ct = default)
    {
        var remote = await _whatsApp.ListTemplatesAsync(ct);
        var updated = 0;

        foreach (var t in remote)
        {
            var entity = await _db.MessageTemplates
                .FirstOrDefaultAsync(x => x.Name == t.Name && x.Language == t.Language, ct);

            var status = t.Status.ToUpperInvariant() switch
            {
                "APPROVED" => TemplateStatus.Approved,
                "REJECTED" => TemplateStatus.Rejected,
                "PAUSED" => TemplateStatus.Paused,
                "DISABLED" => TemplateStatus.Disabled,
                _ => TemplateStatus.Pending
            };

            var category = t.Category.ToUpperInvariant() switch
            {
                "UTILITY" => TemplateCategory.Utility,
                "AUTHENTICATION" => TemplateCategory.Authentication,
                _ => TemplateCategory.Marketing
            };

            var hasMedia = t.ComponentsJson?.Contains("\"HEADER\"", StringComparison.OrdinalIgnoreCase) == true &&
                           (t.ComponentsJson.Contains("IMAGE", StringComparison.OrdinalIgnoreCase) ||
                            t.ComponentsJson.Contains("DOCUMENT", StringComparison.OrdinalIgnoreCase));

            if (entity is null)
            {
                _db.MessageTemplates.Add(new MessageTemplate
                {
                    Name = t.Name,
                    Language = t.Language,
                    Category = category,
                    Status = status,
                    MetaTemplateId = t.Id,
                    ComponentsJson = t.ComponentsJson,
                    HasMediaHeader = hasMedia,
                    LastSyncedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                entity.Status = status;
                entity.Category = category;
                entity.MetaTemplateId = t.Id;
                entity.ComponentsJson = t.ComponentsJson;
                entity.HasMediaHeader = hasMedia;
                entity.LastSyncedAtUtc = DateTime.UtcNow;
                entity.UpdatedAtUtc = DateTime.UtcNow;
            }

            updated++;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.TemplateSync, null, "MessageTemplate", null, $"Synced={updated}", ct: ct);
        _logger.LogInformation("Synced {Count} templates from Meta", updated);
        return updated;
    }
}
