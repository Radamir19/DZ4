using System.Text.Json;
using Orders.Data;
using Orders.DTOs;
using Orders.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Orders.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDbContext _context;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrdersDbContext context, ILogger<OrdersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(CreateOrderRequest request)
    {
        // 1. Атомарно сохраняем заказ и сообщение в Outbox
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Amount = request.Amount,
                Description = request.Description,
                Status = "NEW" // Согласно схеме в ДЗ
            };
            _context.Orders.Add(order);

            var eventData = new { order.Id, order.UserId, order.Amount };
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "OrderCreated",
                Payload = JsonSerializer.Serialize(eventData)
            };
            _context.OutboxMessages.Add(outboxMessage);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Заказ {OrderId} создан и добавлен в Outbox", order.Id);

            // Возвращаем объект через DTO
            return Ok(MapToResponse(order));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Ошибка при создании заказа");
            return StatusCode(500, "Ошибка при создании заказа");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetStatus(Guid id)
    {
        var order = await _context.Orders.FindAsync(id);
        
        if (order == null) return NotFound();

        return Ok(MapToResponse(order));
    }
    
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetUserOrders(string userId)
    {
        var orders = await _context.Orders
            .Where(o => o.UserId == userId)
            .Select(o => MapToResponse(o)) // Маппинг сущности в DTO
            .ToListAsync();

        return Ok(orders);
    }

    // Вспомогательный метод маппинга
    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.UserId,
            order.Amount,
            order.Description,
            order.Status
        );
    }
}