using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payments.Data;
using Payments.DTOs;
using Payments.Models;

namespace Payments.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentsDbContext _context;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(PaymentsDbContext context, ILogger<PaymentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // 1. Создание счета (не более одного на пользователя)
    [HttpPost("account")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        // Проверяем, нет ли уже счета у этого пользователя
        var exists = await _context.Accounts.AnyAsync(a => a.UserId == request.UserId);
        if (exists)
        {
            return BadRequest("Счет для данного пользователя уже существует");
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Balance = 0,
            Version = Guid.NewGuid() // Начальная версия для CAS
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Создан новый счет для пользователя {UserId}", request.UserId);
        return Ok(new { account.Id, account.UserId, account.Balance });
    }

    // 2. Пополнение счета (с защитой от коллизий через CAS)
    [HttpPost("account/topup")]
    public async Task<IActionResult> TopUp([FromBody] TopUpRequest request)
    {
        if (request.Amount <= 0) return BadRequest("Сумма пополнения должна быть больше 0");

        try
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == request.UserId);
            if (account == null) return NotFound("Счет пользователя не найден");

            // Логика CAS в EF Core работает через проверку Concurrency Token (поля Version)
            account.Balance += request.Amount;
            account.Version = Guid.NewGuid(); // Принудительно меняем версию

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Счет пользователя {UserId} пополнен на {Amount}", request.UserId, request.Amount);
            return Ok(new { account.UserId, NewBalance = account.Balance });
        }
        catch (DbUpdateConcurrencyException)
        {
            // Эта ошибка вылетит, если кто-то другой изменил запись в БД 
            // между нашим моментом чтения и моментом сохранения.
            _logger.LogWarning("Коллизия при обновлении баланса пользователя {UserId}", request.UserId);
            return Conflict("Ошибка параллельного доступа. Попробуйте еще раз.");
        }
    }

    // 3. Просмотр баланса счета
    [HttpGet("account/{userId}")]
    public async Task<ActionResult<BalanceResponse>> GetBalance(string userId)
    {
        var account = await _context.Accounts
            .AsNoTracking() // Оптимизация: только чтение
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (account == null) return NotFound("Счет пользователя не найден");

        return Ok(new BalanceResponse(account.UserId, account.Balance));
    }
}