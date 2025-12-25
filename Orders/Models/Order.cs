namespace Orders.Models;

public class Order
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "NEW"; // NEW, FINISHED, CANCELLED
}