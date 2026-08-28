using System.ComponentModel.DataAnnotations;

namespace EcommerceTxPr.Application.Orders.Contracts;

public sealed record CreateOrderItemRequest(
    Guid ProductId,
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    int Quantity);
