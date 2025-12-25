namespace Orders.DTOs;

public record CreateOrderRequest(string UserId, decimal Amount, string Description);