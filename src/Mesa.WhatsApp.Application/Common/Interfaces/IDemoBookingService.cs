using Mesa.WhatsApp.Domain.Enums;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IDemoBookingService
{
    IReadOnlyList<DemoSlotDto> GetAvailableSlots(DateTime fromIst, int daysAhead = 7);
    Task<DemoBookingDto> CreatePendingAsync(Guid leadId, DateTime slotStartIst, CancellationToken ct = default);
    Task<DemoBookingDto> ApproveAsync(Guid bookingId, string userId, CancellationToken ct = default);
    Task<DemoBookingDto> RejectAsync(Guid bookingId, string userId, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<DemoBookingDto>> ListPendingAsync(CancellationToken ct = default);
}

public sealed record DemoSlotDto(string Id, DateTime StartIst, DateTime EndIst, string DisplayLabel);

public sealed record DemoBookingDto(
    Guid Id,
    Guid LeadId,
    string BusinessName,
    DateTime SlotStartIst,
    DateTime SlotEndIst,
    DemoBookingStatus Status,
    string? ApprovedByUserId);
