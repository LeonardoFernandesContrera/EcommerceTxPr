using EcommerceTxPr.Application.Common;

namespace EcommerceTxPr.Application.Customers
{
    public static class CustomerErrors
    {
        public static readonly Error NotFound = new(
            "Customer.NotFound",
            "The customer was not found.");
    }
}
