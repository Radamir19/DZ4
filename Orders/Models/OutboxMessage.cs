namespace Orders.Models;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // Например, "OrderCreated"
    public string Payload { get; set; } = string.Empty; // JSON данные события
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;
}