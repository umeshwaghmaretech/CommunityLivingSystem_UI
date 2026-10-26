using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IAppDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly IAuditService _audit;

    public AuthService(IAppDbContext db, IOptions<JwtOptions> jwt, IAuditService audit)
    {
        _db = db;
        _jwt = jwt.Value;
        _audit = audit;
    }

    public async Task<AuthResult?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant() && u.IsActive, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        user.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.UserLogin, user.Id.ToString(), "AppUser", user.Id.ToString(), null, ct: ct);
        return IssueToken(user);
    }

    public async Task<AuthResult> CreateUserAsync(string email, string displayName, string password, UserRole role, CancellationToken ct = default)
    {
        var normalised = email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == normalised, ct))
            throw new InvalidOperationException("Email already registered.");

        var user = new AppUser
        {
            Email = normalised,
            DisplayName = displayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.UserCreate, user.Id.ToString(), "AppUser", user.Id.ToString(), $"Role={role}", ct: ct);
        return IssueToken(user);
    }

    private AuthResult IssueToken(AppUser user)
    {
        if (string.IsNullOrWhiteSpace(_jwt.SigningKey) || _jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role", user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new AuthResult(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            new JwtSecurityTokenHandler().WriteToken(token),
            expires);
    }
}
