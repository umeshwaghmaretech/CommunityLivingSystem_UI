using Mesa.WhatsApp.Application.Common.Models;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface ILeadImportService
{
    Task<LeadImportResult> ImportFromCsvAsync(Stream csvStream, string? importedByUserId, CancellationToken ct = default);
}

public sealed record LeadImportResult(
    int TotalRows,
    int Imported,
    int Updated,
    int DuplicatesSkipped,
    int Invalid,
    IReadOnlyList<string> Errors);
