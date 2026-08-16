using System.ComponentModel.DataAnnotations;

namespace EcommerceTxPr.Application.Customers.Contracts
{
    public sealed record UpdateCustomerRequest(
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(60, ErrorMessage = "Name must not exceed 60 characters.")]
        string Name,
        [Range(
            typeof(DateTime),
            "0001-01-02",
            "9999-12-31",
            ErrorMessage = "BirthDate must be supplied.")]
        DateTime BirthDate);
}
