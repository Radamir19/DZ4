namespace Payments.Models;

public class OutboxMessage
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Тип события, например "PaymentSucceeded" или "PaymentFailed".
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// JSON-данные события (OrderId, Success и т.д.).
    /// </summary>
    public string Payload { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Флаг, указывающий, было ли сообщение отправлено в RabbitMQ.
    /// </summary>
    public bool IsProcessed { get; set; } = false;
}