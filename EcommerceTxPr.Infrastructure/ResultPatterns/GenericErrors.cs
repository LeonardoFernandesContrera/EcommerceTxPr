using EcommerceTxPr.Domain.Shared;

namespace EcommerceTxPr.Infrastructure.ResultPatterns
{
    public static class GenericErrors
    {
        public static readonly Error NotFoundObject = new("Object not found", "The object was not found in the database");

    }
}
