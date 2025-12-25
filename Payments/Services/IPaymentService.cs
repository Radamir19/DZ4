namespace Payments.Services;

public interface IPaymentService
{
    // Метод для обработки платежа по заказу
    Task ProcessOrderPaymentAsync(Guid orderId, string userId, decimal amount);
}