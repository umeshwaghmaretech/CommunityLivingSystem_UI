namespace Mesa.WhatsApp.Domain.Enums;

public enum AuditAction
{
    MessageSend = 1,
    CampaignCreate = 2,
    CampaignStart = 3,
    CampaignPause = 4,
    CampaignResume = 5,
    CampaignAutoPause = 6,
    LeadImport = 7,
    LeadOptOut = 8,
    LeadBlock = 9,
    DemoApprove = 10,
    DemoReject = 11,
    ManualReply = 12,
    TemplateSync = 13,
    MediaUpload = 14,
    UserLogin = 15,
    UserCreate = 16,
    ReportExport = 17,
    SettingsUpdate = 18
}