namespace Mesa.WhatsApp.Domain.Enums;

public enum MessageStatus
{
    Queued = 1,
    Sending = 2,
    Sent = 3,
    Delivered = 4,
    Read = 5,
    Failed = 6,
    Skipped = 7
}
