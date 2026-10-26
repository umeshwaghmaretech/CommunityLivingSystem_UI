using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mesa.WhatsApp.Infrastructure.WhatsApp;

public sealed class WhatsAppCloudClient : IWhatsAppCloudClient
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _options;
    private readonly CampaignEngineOptions _engineOptions;
    private readonly ILogger<WhatsAppCloudClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WhatsAppCloudClient(
        HttpClient http,
        IOptions<WhatsAppOptions> options,
        IOptions<CampaignEngineOptions> engineOptions,
        ILogger<WhatsAppCloudClient> logger)
    {
        _http = http;
        _options = options.Value;
        _engineOptions = engineOptions.Value;
        _logger = logger;
    }

    public async Task<WhatsAppSendResult> SendTemplateAsync(WhatsAppTemplateSendRequest request, CancellationToken ct = default)
    {
        var components = new JsonArray();

        if (!string.IsNullOrWhiteSpace(request.HeaderMediaId) && request.HeaderMediaKind is not null)
        {
            var mediaType = request.HeaderMediaKind == MediaKind.Document ? "document" : "image";
            components.Add(new JsonObject
            {
                ["type"] = "header",
                ["parameters"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = mediaType,
                        [mediaType] = new JsonObject { ["id"] = request.HeaderMediaId }
                    }
                }
            });
        }

        if (request.BodyParameters.Count > 0)
        {
            var parameters = new JsonArray();
            foreach (var p in request.BodyParameters)
            {
                parameters.Add(new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = p
                });
            }

            components.Add(new JsonObject
            {
                ["type"] = "body",
                ["parameters"] = parameters
            });
        }

        var payload = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = request.ToE164.TrimStart('+'),
            ["type"] = "template",
            ["template"] = new JsonObject
            {
                ["name"] = request.TemplateName,
                ["language"] = new JsonObject { ["code"] = request.LanguageCode },
                ["components"] = components
            }
        };

        return await PostMessageAsync(payload, ct);
    }

    public async Task<WhatsAppSendResult> SendTextAsync(string toE164, string body, CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = toE164.TrimStart('+'),
            ["type"] = "text",
            ["text"] = new JsonObject
            {
                ["preview_url"] = false,
                ["body"] = body
            }
        };

        return await PostMessageAsync(payload, ct);
    }

    public async Task<WhatsAppSendResult> SendInteractiveListAsync(WhatsAppInteractiveListRequest request, CancellationToken ct = default)
    {
        var rows = new JsonArray();
        foreach (var row in request.Rows)
        {
            var obj = new JsonObject
            {
                ["id"] = row.Id,
                ["title"] = Truncate(row.Title, 24)
            };
            if (!string.IsNullOrWhiteSpace(row.Description))
                obj["description"] = Truncate(row.Description, 72);
            rows.Add(obj);
        }

        var payload = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = request.ToE164.TrimStart('+'),
            ["type"] = "interactive",
            ["interactive"] = new JsonObject
            {
                ["type"] = "list",
                ["body"] = new JsonObject { ["text"] = Truncate(request.BodyText, 1024) },
                ["action"] = new JsonObject
                {
                    ["button"] = Truncate(request.ButtonText, 20),
                    ["sections"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["title"] = Truncate(request.SectionTitle, 24),
                            ["rows"] = rows
                        }
                    }
                }
            }
        };

        return await PostMessageAsync(payload, ct);
    }

    public async Task<WhatsAppSendResult> SendInteractiveButtonsAsync(WhatsAppInteractiveButtonsRequest request, CancellationToken ct = default)
    {
        var buttons = new JsonArray();
        foreach (var b in request.Buttons.Take(3))
        {
            buttons.Add(new JsonObject
            {
                ["type"] = "reply",
                ["reply"] = new JsonObject
                {
                    ["id"] = b.Id,
                    ["title"] = Truncate(b.Title, 20)
                }
            });
        }

        var payload = new JsonObject
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = request.ToE164.TrimStart('+'),
            ["type"] = "interactive",
            ["interactive"] = new JsonObject
            {
                ["type"] = "button",
                ["body"] = new JsonObject { ["text"] = Truncate(request.BodyText, 1024) },
                ["action"] = new JsonObject { ["buttons"] = buttons }
            }
        };

        return await PostMessageAsync(payload, ct);
    }

    public async Task<string> UploadMediaAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", fileName);

        var url = $"{BaseUrl()}/{_options.PhoneNumberId}/media";
        using var response = await _http.PostAsync(url, form, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Media upload failed with status {Status}", (int)response.StatusCode);
            throw new InvalidOperationException($"Media upload failed: {(int)response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()
               ?? throw new InvalidOperationException("Media upload response missing id.");
    }

    public async Task<IReadOnlyList<WhatsAppTemplateInfo>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var url = $"{BaseUrl()}/{_options.BusinessAccountId}/message_templates?limit=100";
        using var response = await _http.GetAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Template list failed with status {Status}", (int)response.StatusCode);
            throw new InvalidOperationException($"Template list failed: {(int)response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(json);
        var list = new List<WhatsAppTemplateInfo>();
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return list;

        foreach (var item in data.EnumerateArray())
        {
            list.Add(new WhatsAppTemplateInfo(
                item.GetProperty("id").GetString() ?? string.Empty,
                item.GetProperty("name").GetString() ?? string.Empty,
                item.TryGetProperty("language", out var lang) ? lang.GetString() ?? "en" : "en",
                item.TryGetProperty("status", out var st) ? st.GetString() ?? "PENDING" : "PENDING",
                item.TryGetProperty("category", out var cat) ? cat.GetString() ?? "MARKETING" : "MARKETING",
                item.TryGetProperty("components", out var comps) ? comps.GetRawText() : null));
        }

        return list;
    }

    public async Task<WhatsAppPhoneQualityInfo?> GetPhoneQualityAsync(CancellationToken ct = default)
    {
        var fields = "quality_rating,messaging_limit_tier";
        var url = $"{BaseUrl()}/{_options.PhoneNumberId}?fields={fields}";
        using var response = await _http.GetAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Phone quality fetch failed with status {Status}", (int)response.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var ratingRaw = root.TryGetProperty("quality_rating", out var qr) ? qr.GetString() : null;
        var tier = root.TryGetProperty("messaging_limit_tier", out var mt) ? mt.GetString() : null;

        var rating = ratingRaw?.ToUpperInvariant() switch
        {
            "GREEN" => QualityRating.Green,
            "YELLOW" => QualityRating.Yellow,
            "RED" => QualityRating.Red,
            _ => QualityRating.Unknown
        };

        return new WhatsAppPhoneQualityInfo(rating, tier, json);
    }

    private async Task<WhatsAppSendResult> PostMessageAsync(JsonObject payload, CancellationToken ct)
    {
        var url = $"{BaseUrl()}/{_options.PhoneNumberId}/messages";
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(json);
            string? messageId = null;
            if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                messages.GetArrayLength() > 0)
            {
                messageId = messages[0].GetProperty("id").GetString();
            }

            _logger.LogInformation(
                "WhatsApp message accepted for {PhoneMasked}",
                PhoneNumber.Mask(payload["to"]?.GetValue<string>()));

            return new WhatsAppSendResult(true, messageId, null, null, false, false);
        }

        string? errorCode = null;
        string? errorMessage = "Unknown WhatsApp API error";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.TryGetProperty("code", out var codeEl))
                    errorCode = codeEl.ValueKind == JsonValueKind.Number
                        ? codeEl.GetInt32().ToString()
                        : codeEl.GetString();
                if (err.TryGetProperty("message", out var msgEl))
                    errorMessage = msgEl.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore parse errors
        }

        var codeInt = int.TryParse(errorCode, out var c) ? c : 0;
        var isTransient = _engineOptions.TransientErrorCodes.Contains(codeInt);
        var shouldPause = _engineOptions.SpamOrRateLimitErrorCodes.Contains(codeInt);

        _logger.LogWarning(
            "WhatsApp send failed code={Code} transient={Transient} pause={Pause} phone={Phone}",
            errorCode,
            isTransient,
            shouldPause,
            PhoneNumber.Mask(payload["to"]?.GetValue<string>()));

        return new WhatsAppSendResult(false, null, errorCode, errorMessage, isTransient, shouldPause);
    }

    private string BaseUrl() =>
        $"{_options.GraphApiBaseUrl.TrimEnd('/')}/{_options.GraphApiVersion.TrimStart('v').Insert(0, "v")}";

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
