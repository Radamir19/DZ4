namespace Orders.DTOs;

/// <summary>
/// Объект ответа, содержащий информацию о заказе.
/// </summary>
public record OrderResponse(
    Guid Id,
    string UserId,
    decimal Amount,
    string Description,
    string Status
);