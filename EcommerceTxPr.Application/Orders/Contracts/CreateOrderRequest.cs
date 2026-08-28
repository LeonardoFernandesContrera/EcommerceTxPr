using System.ComponentModel.DataAnnotations;

namespace EcommerceTxPr.Application.Orders.Contracts;

public sealed record CreateOrderRequest(
    Guid CustomerId,
    [Required(ErrorMessage = "Items are required.")]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    IReadOnlyCollection<CreateOrderItemRequest> Items);
