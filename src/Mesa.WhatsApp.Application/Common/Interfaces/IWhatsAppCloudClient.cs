using Mesa.WhatsApp.Application.Common.Models;
using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IWhatsAppCloudClient
{
    Task<WhatsAppSendResult> SendTemplateAsync(WhatsAppTemplateSendRequest request, CancellationToken ct = default);
    Task<WhatsAppSendResult> SendTextAsync(string toE164, string body, CancellationToken ct = default);
    Task<WhatsAppSendResult> SendInteractiveListAsync(WhatsAppInteractiveListRequest request, CancellationToken ct = default);
    Task<WhatsAppSendResult> SendInteractiveButtonsAsync(WhatsAppInteractiveButtonsRequest request, CancellationToken ct = default);
    Task<string> UploadMediaAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<IReadOnlyList<WhatsAppTemplateInfo>> ListTemplatesAsync(CancellationToken ct = default);
    Task<WhatsAppPhoneQualityInfo?> GetPhoneQualityAsync(CancellationToken ct = default);
}

public sealed record WhatsAppTemplateSendRequest(
    string ToE164,
    string TemplateName,
    string LanguageCode,
    IReadOnlyList<string> BodyParameters,
    string? HeaderMediaId = null,
    MediaKind? HeaderMediaKind = null);

public sealed record WhatsAppInteractiveListRequest(
    string ToE164,
    string BodyText,
    string ButtonText,
    string SectionTitle,
    IReadOnlyList<WhatsAppListRow> Rows);

public sealed record WhatsAppListRow(string Id, string Title, string? Description);

public sealed record WhatsAppInteractiveButtonsRequest(
    string ToE164,
    string BodyText,
    IReadOnlyList<WhatsAppButton> Buttons);

public sealed record WhatsAppButton(string Id, string Title);

public sealed record WhatsAppSendResult(
    bool Success,
    string? MessageId,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsTransient,
    bool ShouldPauseCampaign);

public sealed record WhatsAppTemplateInfo(
    string Id,
    string Name,
    string Language,
    string Status,
    string Category,
    string? ComponentsJson);

public sealed record WhatsAppPhoneQualityInfo(
    QualityRating QualityRating,
    string? MessagingLimitTier,
    string? RawJson);
