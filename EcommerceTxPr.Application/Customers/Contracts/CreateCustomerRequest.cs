namespace EcommerceTxPr.Application.Customers.Contracts
{
    public sealed record CreateCustomerRequest(string Name, DateTime BirthDate);
}
