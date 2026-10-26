using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IInboxService
{
    Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsNeedingHumanAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MessageDto>> GetConversationAsync(Guid leadId, CancellationToken ct = default);
    Task<MessageDto> SendManualReplyAsync(Guid leadId, string body, string userId, CancellationToken ct = default);
    Task FlagForHumanAsync(Guid leadId, bool needsHuman, CancellationToken ct = default);
}

public sealed record ConversationSummaryDto(
    Guid LeadId,
    string BusinessName,
    string PhoneMasked,
    LeadStatus Status,
    bool NeedsHumanHandover,
    DateTime? LastInboundAtUtc,
    string? LastMessagePreview);

public sealed record MessageDto(
    Guid Id,
    MessageDirection Direction,
    MessageType Type,
    MessageStatus Status,
    string? Body,
    DateTime CreatedAtUtc,
    string? SentByUserId);
