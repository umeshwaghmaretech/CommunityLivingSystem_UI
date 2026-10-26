using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mesa.WhatsApp.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<BlockedNumber> BlockedNumbers => Set<BlockedNumber>();
    public DbSet<DoNotContactEntry> DoNotContactEntries => Set<DoNotContactEntry>();
    public DbSet<PriorityRule> PriorityRules => Set<PriorityRule>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<TemplateVariable> TemplateVariables => Set<TemplateVariable>();
    public DbSet<SegmentMedia> SegmentMedia => Set<SegmentMedia>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<DemoBooking> DemoBookings => Set<DemoBooking>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccountQualitySnapshot> AccountQualitySnapshots => Set<AccountQualitySnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Lead>(e =>
        {
            e.HasIndex(x => x.PhoneE164).IsUnique();
            e.Property(x => x.BusinessName).HasMaxLength(256).IsRequired();
            e.Property(x => x.ContactName).HasMaxLength(256);
            e.Property(x => x.PhoneE164).HasMaxLength(20).IsRequired();
            e.Property(x => x.City).HasMaxLength(128);
            e.Property(x => x.Pincode).HasMaxLength(16);
            e.Property(x => x.Source).HasMaxLength(128);
        });

        modelBuilder.Entity<BlockedNumber>(e =>
        {
            e.HasIndex(x => x.PhoneE164).IsUnique();
            e.Property(x => x.PhoneE164).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<DoNotContactEntry>(e =>
        {
            e.HasIndex(x => x.PhoneE164).IsUnique();
            e.Property(x => x.PhoneE164).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<PriorityRule>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Pincode).HasMaxLength(16);
            e.Property(x => x.City).HasMaxLength(128);
        });

        modelBuilder.Entity<MessageTemplate>(e =>
        {
            e.HasIndex(x => new { x.Name, x.Language }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(512).IsRequired();
            e.Property(x => x.Language).HasMaxLength(16).IsRequired();
        });

        modelBuilder.Entity<TemplateVariable>(e =>
        {
            e.HasOne(x => x.MessageTemplate)
                .WithMany(x => x.Variables)
                .HasForeignKey(x => x.MessageTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SegmentMedia>(e =>
        {
            e.HasIndex(x => new { x.Segment, x.Kind }).IsUnique();
            e.Property(x => x.FileName).HasMaxLength(256);
            e.Property(x => x.ContentType).HasMaxLength(128);
            e.Property(x => x.WhatsAppMediaId).HasMaxLength(256);
        });

        modelBuilder.Entity<Campaign>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.TimeZoneId).HasMaxLength(64);
            e.HasOne(x => x.MessageTemplate)
                .WithMany()
                .HasForeignKey(x => x.MessageTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FollowUpTemplate)
                .WithMany()
                .HasForeignKey(x => x.FollowUpTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CampaignRecipient>(e =>
        {
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.CampaignId, x.LeadId, x.FollowUpNumber }).IsUnique();
            e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.Campaign)
                .WithMany(x => x.Recipients)
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Lead)
                .WithMany(x => x.CampaignRecipients)
                .HasForeignKey(x => x.LeadId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConversationMessage>(e =>
        {
            e.HasIndex(x => x.WhatsAppMessageId);
            e.HasIndex(x => x.LeadId);
            e.HasOne(x => x.Lead)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemoBooking>(e =>
        {
            e.HasOne(x => x.Lead)
                .WithMany(x => x.DemoBookings)
                .HasForeignKey(x => x.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(x => x.EntityType).HasMaxLength(128);
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
