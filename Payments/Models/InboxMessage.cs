namespace Payments.Models;

public class InboxMessage
{
    /// <summary>
    /// Сюда записывается ID входящего сообщения (OrderId).
    /// Это гарантирует, что мы не спишем деньги за один и тот же заказ дважды.
    /// </summary>
    public Guid Id { get; set; }
    
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}