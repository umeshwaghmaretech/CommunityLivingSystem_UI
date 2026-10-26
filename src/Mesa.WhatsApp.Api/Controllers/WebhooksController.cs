using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Api.Controllers;

[ApiController]
[Route("api/webhooks/whatsapp")]
[AllowAnonymous]
[EnableRateLimiting("webhook")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookSignatureValidator _signatureValidator;
    private readonly IWebhookProcessor _processor;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IWebhookSignatureValidator signatureValidator,
        IWebhookProcessor processor,
        IOptions<WhatsAppOptions> options,
        ILogger<WebhooksController> logger)
    {
        _signatureValidator = signatureValidator;
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Meta webhook verification (hub.challenge).</summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" &&
            !string.IsNullOrEmpty(verifyToken) &&
            verifyToken == _options.VerifyToken)
        {
            return Content(challenge ?? string.Empty, "text/plain");
        }

        return Unauthorized();
    }

    /// <summary>Inbound webhook events with X-Hub-Signature-256 validation.</summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (!_signatureValidator.IsValid(rawBody, signature))
        {
            _logger.LogWarning("Webhook signature validation failed");
            return Unauthorized();
        }

        try
        {
            await _processor.ProcessAsync(rawBody, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook processing error");
            // Return 200 to avoid Meta retry storms for poison payloads after signature OK
        }

        return Ok();
    }
}
