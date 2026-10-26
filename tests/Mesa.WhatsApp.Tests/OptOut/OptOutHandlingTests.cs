using Mesa.WhatsApp.Application.Common.Interfaces;
using Mesa.WhatsApp.Application.Options;
using Mesa.WhatsApp.Domain.Entities;
using Mesa.WhatsApp.Domain.Enums;
using Mesa.WhatsApp.Infrastructure.Persistence;
using Mesa.WhatsApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Mesa.WhatsApp.Tests.OptOut;

public class OptOutHandlingTests
{
    [Fact]
    public async Task InboundStop_MarksOptedOut_AddsDnc_SendsConfirmation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);

        var lead = new Lead
        {
            BusinessName = "Spice Garden",
            ContactName = "Ravi",
            PhoneE164 = "+919876543210",
            Segment = BusinessSegment.Restaurant,
            Status = LeadStatus.Contacted
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var whatsApp = new Mock<IWhatsAppCloudClient>();
        whatsApp.Setup(w => w.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatsAppSendResult(true, "wamid.test", null, null, false, false));

        var demos = new Mock<IDemoBookingService>();
        var processor = new WebhookProcessor(
            db,
            whatsApp.Object,
            demos.Object,
            Mock.Of<IAuditService>(),
            Options.Create(new CampaignEngineOptions()),
            NullLogger<WebhookProcessor>.Instance);

        var payload = """
        {
          "entry": [{
            "changes": [{
              "value": {
                "messages": [{
                  "from": "919876543210",
                  "id": "wamid.inbound1",
                  "type": "text",
                  "text": { "body": "STOP" }
                }]
              }
            }]
          }]
        }
        """;

        await processor.ProcessAsync(payload);

        var updated = await db.Leads.FirstAsync(l => l.Id == lead.Id);
        Assert.True(updated.IsOptedOut);
        Assert.Equal(LeadStatus.OptedOut, updated.Status);
        Assert.True(await db.DoNotContactEntries.AnyAsync(x => x.PhoneE164 == lead.PhoneE164));
        whatsApp.Verify(w => w.SendTextAsync(
            lead.PhoneE164,
            It.Is<string>(s => s.Contains("unsubscribed", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
