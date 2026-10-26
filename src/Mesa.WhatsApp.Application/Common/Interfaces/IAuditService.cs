using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        AuditAction action,
        string? userId,
        string? entityType,
        string? entityId,
        string? details,
        string? ipAddress = null,
        CancellationToken ct = default);
}
