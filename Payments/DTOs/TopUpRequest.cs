namespace Payments.DTOs;

public record TopUpRequest(string UserId, decimal Amount);