namespace EcommerceTxPr.Application.Common
{
    public sealed record Error(string Code, string Message, ErrorType Type);
}
