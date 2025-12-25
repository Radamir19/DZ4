using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Payments.Data;
using Payments.Models;

namespace Payments.Services;

public class PaymentService : IPaymentService
{
    private readonly PaymentsDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(PaymentsDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ProcessOrderPaymentAsync(Guid orderId, string userId, decimal amount)
    {
        // 1. Начинаем транзакцию (Атомарность всех действий)
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // --- ПАТТЕРН TRANSACTIONAL INBOX ---
            // Проверяем, не обрабатывали ли мы этот заказ ранее
            var alreadyProcessed = await _context.InboxMessages.AnyAsync(i => i.Id == orderId);
            if (alreadyProcessed)
            {
                _logger.LogWarning("Заказ {OrderId} уже был обработан. Игнорируем дубликат.", orderId);
                return; // Effectively exactly once: просто выходим
            }

            // 2. Ищем счет пользователя
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);
            
            bool isSuccess = false;
            string reason = "";

            if (account == null)
            {
                reason = "Счет не найден";
            }
            else if (account.Balance < amount)
            {
                reason = "Недостаточно средств";
            }
            else
            {
                // --- ПАТТЕРН CAS (Compare and Swap) ---
                // Уменьшаем баланс и обновляем версию для контроля конкуренции
                account.Balance -= amount;
                account.Version = Guid.NewGuid(); // EF Core проверит старую версию при сохранении
                isSuccess = true;
            }

            // 3. Записываем ID заказа в Inbox (помечаем как обработанный)
            _context.InboxMessages.Add(new InboxMessage { Id = orderId });

            // --- ПАТТЕРН TRANSACTIONAL OUTBOX ---
            // Формируем результат для отправки обратно в Orders Service
            var resultPayload = new { OrderId = orderId, Success = isSuccess, Message = reason };
            _context.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "PaymentResult",
                Payload = JsonSerializer.Serialize(resultPayload)
            });

            // 4. Сохраняем всё в базу данных
            // Если на этом этапе кто-то другой изменил баланс, EF выкинет DbUpdateConcurrencyException (CAS)
            await _context.SaveChangesAsync();

            // Фиксируем транзакцию
            await transaction.CommitAsync();
            
            _logger.LogInformation("Платеж по заказу {OrderId} обработан. Результат: {Result}", orderId, isSuccess);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Ошибка CAS: кто-то параллельно изменил баланс
            await transaction.RollbackAsync();
            _logger.LogError("Конфликт параллельного доступа при оплате заказа {OrderId}. Откат.", orderId);
            throw; // Позволяем RabbitMQConsumer попробовать обработать сообщение позже (Retry)
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Ошибка при обработке платежа для заказа {OrderId}", orderId);
            throw;
        }
    }
}