namespace EcommerceTxPr.Domain.Shared
{
    public sealed record Error(string Code, string? Message = null);
}
