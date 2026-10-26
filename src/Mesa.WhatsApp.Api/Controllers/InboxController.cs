using System.Security.Claims;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/inbox")]
[Authorize(Policy = "SalesOrAbove")]
public class InboxController : ControllerBase
{
    private readonly IInboxService _inbox;

    public InboxController(IInboxService inbox) => _inbox = inbox;

    [HttpGet("handover")]
    public async Task<IActionResult> NeedingHuman(CancellationToken ct) =>
        Ok(await _inbox.GetConversationsNeedingHumanAsync(ct));

    [HttpGet("{leadId:guid}")]
    public async Task<IActionResult> Conversation(Guid leadId, CancellationToken ct) =>
        Ok(await _inbox.GetConversationAsync(leadId, ct));

    public sealed record ReplyRequest(string Body);

    [HttpPost("{leadId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid leadId, [FromBody] ReplyRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        try
        {
            var msg = await _inbox.SendManualReplyAsync(leadId, request.Body, userId, ct);
            return Ok(msg);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{leadId:guid}/flag")]
    public async Task<IActionResult> Flag(Guid leadId, [FromQuery] bool needsHuman = true, CancellationToken ct = default)
    {
        await _inbox.FlagForHumanAsync(leadId, needsHuman, ct);
        return Ok();
    }
}
