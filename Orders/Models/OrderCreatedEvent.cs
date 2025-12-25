namespace Orders.Models;

public record OrderCreatedEvent(Guid Id, string UserId, decimal Amount);