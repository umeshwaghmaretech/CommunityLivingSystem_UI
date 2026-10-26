using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResult?> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> CreateUserAsync(string email, string displayName, string password, UserRole role, CancellationToken ct = default);
}

public sealed record AuthResult(
    Guid UserId,
    string Email,
    string DisplayName,
    UserRole Role,
    string AccessToken,
    DateTime ExpiresAtUtc);
