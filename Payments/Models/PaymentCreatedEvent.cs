namespace Payments.Models;

public record PaymentResultEvent(Guid OrderId, bool Success, string Message);