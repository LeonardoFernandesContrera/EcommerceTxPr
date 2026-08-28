namespace EcommerceTxPr.Application.Customers.Contracts
{
    public sealed record CustomerResponse(
        Guid Id,
        string Name,
        DateTime BirthDate,
        DateTime CreationDate);
}
