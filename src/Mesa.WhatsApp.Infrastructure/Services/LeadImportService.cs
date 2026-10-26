using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Common.Models;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mesa.WhatsApp.Infrastructure.Services;

public sealed class LeadImportService : ILeadImportService
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<LeadImportService> _logger;

    public LeadImportService(IAppDbContext db, IAuditService audit, ILogger<LeadImportService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<LeadImportResult> ImportFromCsvAsync(Stream csvStream, string? importedByUserId, CancellationToken ct = default)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant().Replace(" ", "").Replace("_", "")
        };

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<LeadImportRowMap>();

        var rows = csv.GetRecords<LeadImportRow>().ToList();
        var errors = new List<string>();
        var imported = 0;
        var updated = 0;
        var duplicates = 0;
        var invalid = 0;

        var existingPhones = await _db.Leads.AsNoTracking()
            .Select(l => l.PhoneE164)
            .ToListAsync(ct);
        var phoneSet = existingPhones.ToHashSet(StringComparer.Ordinal);

        var dnc = await _db.DoNotContactEntries.AsNoTracking().Select(x => x.PhoneE164).ToListAsync(ct);
        var blocked = await _db.BlockedNumbers.AsNoTracking().Select(x => x.PhoneE164).ToListAsync(ct);
        var skipSet = dnc.Concat(blocked).ToHashSet(StringComparer.Ordinal);

        var priorityRules = await _db.PriorityRules.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        var seenInFile = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (row, index) in rows.Select((r, i) => (r, i + 2)))
        {
            if (!PhoneNumber.TryCreate(row.Phone, out var phone, out var phoneError) || phone is null)
            {
                invalid++;
                errors.Add($"Row {index}: {phoneError}");
                continue;
            }

            if (!SegmentParser.TryParse(row.Segment, out var segment))
            {
                invalid++;
                errors.Add($"Row {index}: unknown segment '{row.Segment}'");
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.BusinessName))
            {
                invalid++;
                errors.Add($"Row {index}: business name is required");
                continue;
            }

            if (!seenInFile.Add(phone.E164))
            {
                duplicates++;
                continue;
            }

            if (skipSet.Contains(phone.E164))
            {
                duplicates++;
                errors.Add($"Row {index}: phone is on DNC/blocked list — skipped ({PhoneNumber.Mask(phone.E164)})");
                continue;
            }

            var status = LeadStatus.New;
            if (!string.IsNullOrWhiteSpace(row.Status) &&
                Enum.TryParse<LeadStatus>(row.Status.Replace(" ", ""), true, out var parsedStatus))
            {
                status = parsedStatus;
            }

            if (phoneSet.Contains(phone.E164))
            {
                var existing = await _db.Leads.FirstAsync(l => l.PhoneE164 == phone.E164, ct);
                if (existing.IsOptedOut || existing.IsBlocked)
                {
                    duplicates++;
                    continue;
                }

                existing.BusinessName = row.BusinessName.Trim();
                existing.ContactName = (row.ContactName ?? string.Empty).Trim();
                existing.City = row.City?.Trim();
                existing.Pincode = row.Pincode?.Trim();
                existing.Segment = segment;
                existing.Source = row.Source?.Trim() ?? existing.Source;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else
            {
                _db.Leads.Add(new Lead
                {
                    BusinessName = row.BusinessName.Trim(),
                    ContactName = (row.ContactName ?? string.Empty).Trim(),
                    PhoneE164 = phone.E164,
                    City = row.City?.Trim(),
                    Pincode = row.Pincode?.Trim(),
                    Segment = segment,
                    Source = row.Source?.Trim() ?? "csv_import",
                    Status = status
                });
                phoneSet.Add(phone.E164);
                imported++;
            }
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            AuditAction.LeadImport,
            importedByUserId,
            "Lead",
            null,
            $"Imported={imported}, Updated={updated}, Dupes={duplicates}, Invalid={invalid}",
            ct: ct);

        _logger.LogInformation(
            "Lead import finished: imported={Imported} updated={Updated} dupes={Dupes} invalid={Invalid}",
            imported, updated, duplicates, invalid);

        // priorityRules reserved for campaign enqueue ordering
        _ = priorityRules;

        return new LeadImportResult(rows.Count, imported, updated, duplicates, invalid, errors.Take(50).ToList());
    }

    private sealed class LeadImportRowMap : ClassMap<LeadImportRow>
    {
        public LeadImportRowMap()
        {
            Map(m => m.BusinessName).Name("businessname", "business_name", "Business Name", "business");
            Map(m => m.ContactName).Name("contactname", "contact_name", "Contact Name", "contact");
            Map(m => m.Phone).Name("phone", "phone_number", "Phone", "mobile");
            Map(m => m.City).Name("city", "City");
            Map(m => m.Pincode).Name("pincode", "pin_code", "Pincode", "pin");
            Map(m => m.Segment).Name("segment", "Segment");
            Map(m => m.Source).Name("source", "Source");
            Map(m => m.Status).Name("status", "Status");
        }
    }
}
