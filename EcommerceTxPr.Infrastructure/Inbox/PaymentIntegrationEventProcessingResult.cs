namespace EcommerceTxPr.Infrastructure.Inbox;

public enum PaymentIntegrationEventProcessingResult
{
    Processed = 0,
    Duplicate = 1,
    Poison = 2
}
