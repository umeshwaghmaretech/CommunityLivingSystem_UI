using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly IAppDbContext _db;

    public AuditService(IAppDbContext db) => _db = db;

    public async Task LogAsync(
        AuditAction action,
        string? userId,
        string? entityType,
        string? entityId,
        string? details,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync(ct);
    }
}
