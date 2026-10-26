using Mesa.WhatsApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Lead> Leads { get; }
    DbSet<BlockedNumber> BlockedNumbers { get; }
    DbSet<DoNotContactEntry> DoNotContactEntries { get; }
    DbSet<PriorityRule> PriorityRules { get; }
    DbSet<MessageTemplate> MessageTemplates { get; }
    DbSet<TemplateVariable> TemplateVariables { get; }
    DbSet<SegmentMedia> SegmentMedia { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<CampaignRecipient> CampaignRecipients { get; }
    DbSet<ConversationMessage> ConversationMessages { get; }
    DbSet<DemoBooking> DemoBookings { get; }
    DbSet<AppUser> Users { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AccountQualitySnapshot> AccountQualitySnapshots { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
