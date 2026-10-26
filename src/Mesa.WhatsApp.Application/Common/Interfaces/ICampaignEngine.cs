namespace Mesa.WhatsApp.Application.Common.Interfaces;

public interface ICampaignEngine
{
    Task EnqueueEligibleRecipientsAsync(Guid campaignId, CancellationToken ct = default);
    Task ProcessRecipientSendAsync(Guid campaignRecipientId, CancellationToken ct = default);
    Task EvaluateAutoPauseAsync(Guid campaignId, CancellationToken ct = default);
    Task ProcessFollowUpsAsync(CancellationToken ct = default);
    bool IsWithinSendWindow(Domain.Entities.Campaign campaign, DateTime utcNow);
    int ComputePacingDelayMs(Domain.Entities.Campaign campaign, Random? random = null);
}
