using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class DemoBookingService : IDemoBookingService
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private static readonly TimeZoneInfo Ist = ResolveIst();

    public DemoBookingService(IAppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public IReadOnlyList<DemoSlotDto> GetAvailableSlots(DateTime fromUtc, int daysAhead = 7)
    {
        var fromIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), Ist);
        var slots = new List<DemoSlotDto>();

        for (var day = 0; day < daysAhead; day++)
        {
            var date = fromIst.Date.AddDays(day);
            // Any day 15:00–18:00 IST — offer hourly starts
            for (var hour = 15; hour < 18; hour++)
            {
                var start = date.AddHours(hour);
                if (start <= fromIst)
                    continue;

                var end = start.AddHours(1);
                var id = $"demo_slot_{start:yyyyMMdd}_{start:HHmm}";
                slots.Add(new DemoSlotDto(
                    id,
                    start,
                    end,
                    start.ToString("ddd dd MMM HH:mm")));
            }
        }

        return slots;
    }

    public async Task<DemoBookingDto> CreatePendingAsync(Guid leadId, DateTime slotStartIst, CancellationToken ct = default)
    {
        var lead = await _db.Leads.FirstAsync(l => l.Id == leadId, ct);
        var booking = new DemoBooking
        {
            LeadId = leadId,
            SlotStartIst = slotStartIst,
            SlotEndIst = slotStartIst.AddHours(1),
            Status = DemoBookingStatus.PendingApproval
        };
        _db.DemoBookings.Add(booking);
        lead.Status = LeadStatus.DemoRequested;
        lead.NeedsHumanHandover = true;
        await _db.SaveChangesAsync(ct);

        return ToDto(booking, lead.BusinessName);
    }

    public async Task<DemoBookingDto> ApproveAsync(Guid bookingId, string userId, CancellationToken ct = default)
    {
        var booking = await _db.DemoBookings.Include(b => b.Lead).FirstAsync(b => b.Id == bookingId, ct);
        booking.Status = DemoBookingStatus.Approved;
        booking.ApprovedByUserId = userId;
        booking.ApprovedAtUtc = DateTime.UtcNow;
        booking.Lead.Status = LeadStatus.DemoBooked;
        booking.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.DemoApprove, userId, "DemoBooking", bookingId.ToString(), null, ct: ct);
        return ToDto(booking, booking.Lead.BusinessName);
    }

    public async Task<DemoBookingDto> RejectAsync(Guid bookingId, string userId, string? reason, CancellationToken ct = default)
    {
        var booking = await _db.DemoBookings.Include(b => b.Lead).FirstAsync(b => b.Id == bookingId, ct);
        booking.Status = DemoBookingStatus.Rejected;
        booking.RejectionReason = reason;
        booking.ApprovedByUserId = userId;
        booking.ApprovedAtUtc = DateTime.UtcNow;
        booking.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.DemoReject, userId, "DemoBooking", bookingId.ToString(), reason, ct: ct);
        return ToDto(booking, booking.Lead.BusinessName);
    }

    public async Task<IReadOnlyList<DemoBookingDto>> ListPendingAsync(CancellationToken ct = default)
    {
        return await _db.DemoBookings.AsNoTracking()
            .Include(b => b.Lead)
            .Where(b => b.Status == DemoBookingStatus.PendingApproval)
            .OrderBy(b => b.SlotStartIst)
            .Select(b => new DemoBookingDto(
                b.Id, b.LeadId, b.Lead.BusinessName, b.SlotStartIst, b.SlotEndIst, b.Status, b.ApprovedByUserId))
            .ToListAsync(ct);
    }

    private static DemoBookingDto ToDto(DemoBooking b, string businessName) =>
        new(b.Id, b.LeadId, businessName, b.SlotStartIst, b.SlotEndIst, b.Status, b.ApprovedByUserId);

    private static TimeZoneInfo ResolveIst()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST"); }
        }
    }
}
