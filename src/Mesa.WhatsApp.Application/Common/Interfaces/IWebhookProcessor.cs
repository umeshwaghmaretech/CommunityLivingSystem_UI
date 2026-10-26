namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IWebhookProcessor
{
    Task ProcessAsync(string rawJson, CancellationToken ct = default);
}

public interface IWebhookSignatureValidator
{
    bool IsValid(string rawBody, string? signatureHeader);
}
