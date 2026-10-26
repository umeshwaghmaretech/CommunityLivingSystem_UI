using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mesa.WhatsApp.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.EnsureCreatedAsync(ct);

        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.AddRange(
                new AppUser
                {
                    Email = "owner@ubnexora.local",
                    DisplayName = "MESA Owner",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe!Owner1"),
                    Role = UserRole.Owner
                },
                new AppUser
                {
                    Email = "manager@ubnexora.local",
                    DisplayName = "MESA Manager",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe!Manager1"),
                    Role = UserRole.Manager
                },
                new AppUser
                {
                    Email = "sales@ubnexora.local",
                    DisplayName = "Sales Agent",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe!Sales1"),
                    Role = UserRole.SalesAgent
                },
                new AppUser
                {
                    Email = "viewer@ubnexora.local",
                    DisplayName = "Viewer",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe!Viewer1"),
                    Role = UserRole.Viewer
                });
        }

        if (!await db.PriorityRules.AnyAsync(ct))
        {
            db.PriorityRules.Add(new PriorityRule
            {
                Name = "Aurangabad HQ pincode first",
                Pincode = "431001",
                Priority = 1,
                IsActive = true
            });
        }

        if (!await db.MessageTemplates.AnyAsync(ct))
        {
            var outreach = new MessageTemplate
            {
                Name = "mesa_outreach_v1",
                Language = "en",
                Category = TemplateCategory.Marketing,
                Status = TemplateStatus.Approved,
                HasMediaHeader = true,
                BodyText =
                    "Dear {{1}} team,\n\nMESA is the Operating System for Restaurants — digitise orders, tables and payments. Scan the QR or tap NFC, no app needed.\n\nLet's connect!\nTeam UB Nexora Technologies | +91 8855008116",
                FooterText = "Team UB Nexora Technologies | +91 8855008116"
            };
            outreach.Variables.Add(new TemplateVariable
            {
                Index = 1,
                Placeholder = "{{1}}",
                MappedField = "business_name",
                ExampleValue = "Spice Garden"
            });

            var followUp = new MessageTemplate
            {
                Name = "mesa_followup_v1",
                Language = "en",
                Category = TemplateCategory.Marketing,
                Status = TemplateStatus.Approved,
                HasMediaHeader = false,
                BodyText =
                    "Dear {{1}} team,\n\nJust checking in about MESA. Scan the QR or tap NFC, no app needed.\n\nLet's connect!\nTeam UB Nexora Technologies | +91 8855008116"
            };
            followUp.Variables.Add(new TemplateVariable
            {
                Index = 1,
                Placeholder = "{{1}}",
                MappedField = "business_name",
                ExampleValue = "Spice Garden"
            });

            db.MessageTemplates.AddRange(outreach, followUp);
        }

        if (!await db.Leads.AnyAsync(ct))
        {
            db.Leads.AddRange(
                new Lead
                {
                    BusinessName = "Spice Garden",
                    ContactName = "Ravi Patil",
                    PhoneE164 = "+919876543210",
                    City = "Aurangabad",
                    Pincode = "431001",
                    Segment = BusinessSegment.Restaurant,
                    Source = "seed"
                },
                new Lead
                {
                    BusinessName = "Cafe Mocha",
                    ContactName = "Sneha Deshmukh",
                    PhoneE164 = "+919876543211",
                    City = "Pune",
                    Pincode = "411001",
                    Segment = BusinessSegment.Cafe,
                    Source = "seed"
                },
                new Lead
                {
                    BusinessName = "Highway Dhaba",
                    ContactName = "Amit Shah",
                    PhoneE164 = "+919876543212",
                    City = "Nashik",
                    Pincode = "422001",
                    Segment = BusinessSegment.Dhaba,
                    Source = "seed"
                });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Database seed completed");
    }
}
