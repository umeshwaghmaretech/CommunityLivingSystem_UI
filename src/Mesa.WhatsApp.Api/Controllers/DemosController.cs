using System.Security.Claims;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/demos")]
[Authorize(Policy = "SalesOrAbove")]
public class DemosController : ControllerBase
{
    private readonly IDemoBookingService _demos;

    public DemosController(IDemoBookingService demos) => _demos = demos;

    [HttpGet("slots")]
    public IActionResult Slots([FromQuery] int daysAhead = 7) =>
        Ok(_demos.GetAvailableSlots(DateTime.UtcNow, daysAhead));

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct) =>
        Ok(await _demos.ListPendingAsync(ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub") ?? "unknown";
        return Ok(await _demos.ApproveAsync(id, userId, ct));
    }

    public sealed record RejectRequest(string? Reason);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "ManagerOrAbove")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest? request, CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub") ?? "unknown";
        return Ok(await _demos.RejectAsync(id, userId, request?.Reason, ct));
    }
}
