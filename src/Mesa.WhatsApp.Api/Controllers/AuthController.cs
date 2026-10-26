using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    public sealed record LoginRequest(string Email, string Password);
    public sealed record CreateUserRequest(string Email, string DisplayName, string Password, UserRole Role);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request.Email, request.Password, ct);
        if (result is null)
            return Unauthorized(new { message = "Invalid credentials" });
        return Ok(result);
    }

    [HttpPost("users")]
    [Authorize(Policy = "Owner")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _auth.CreateUserAsync(request.Email, request.DisplayName, request.Password, request.Role, ct);
        return Ok(result);
    }
}
