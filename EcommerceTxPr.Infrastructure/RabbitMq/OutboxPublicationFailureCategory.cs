namespace EcommerceTxPr.Infrastructure.RabbitMq;

public enum OutboxPublicationFailureCategory
{
    Connection = 0,
    Channel = 1,
    Topology = 2,
    ConfirmationOrRouting = 3,
    Publish = 4
}
