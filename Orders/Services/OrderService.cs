using Orders.Data;
using Microsoft.EntityFrameworkCore;

namespace Orders.Services;

public interface IOrderService
{
    Task UpdateOrderStatusAsync(Guid orderId, string newStatus);
}

public class OrderService : IOrderService
{
    private readonly OrdersDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrdersDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, string newStatus)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order != null)
        {
            order.Status = newStatus;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Заказ {OrderId} обновлен до статуса {Status}", orderId, newStatus);
        }
        else
        {
            _logger.LogWarning("Заказ {OrderId} не найден для обновления статуса", orderId);
        }
    }
}