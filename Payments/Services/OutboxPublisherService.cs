using System.Text;
using Microsoft.EntityFrameworkCore;
using Payments.Data;
using RabbitMQ.Client;

namespace Payments.Services;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<OutboxPublisherService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public OutboxPublisherService(
        IServiceProvider serviceProvider, 
        IConfiguration config, 
        ILogger<OutboxPublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    private async Task InitializeRabbitMQAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"] ?? "localhost"
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Объявляем очередь, куда будем слать результаты оплаты
            await _channel.QueueDeclareAsync(
                queue: "payment_results_queue", 
                durable: true, 
                exclusive: false, 
                autoDelete: false, 
                cancellationToken: stoppingToken);
            
            _logger.LogInformation("OutboxPublisher (Payments) подключен к RabbitMQ.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации RabbitMQ в Payments Service.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeRabbitMQAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_channel == null || !_channel.IsOpen)
            {
                await InitializeRabbitMQAsync(stoppingToken);
                await Task.Delay(5000, stoppingToken);
                continue;
            }

            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

                    // Выбираем пачку необработанных сообщений об итогах оплаты
                    var messages = await dbContext.OutboxMessages
                        .Where(m => !m.IsProcessed)
                        .OrderBy(m => m.CreatedAt)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    if (messages.Any())
                    {
                        foreach (var message in messages)
                        {
                            var body = Encoding.UTF8.GetBytes(message.Payload);

                            // Публикуем ответ в очередь результатов
                            await _channel.BasicPublishAsync(
                                exchange: string.Empty,
                                routingKey: "payment_results_queue",
                                body: body,
                                cancellationToken: stoppingToken);

                            message.IsProcessed = true;
                        }

                        // Сохраняем статус "обработано" в БД
                        await dbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Отправлено {Count} ответов об оплате в Orders Service", messages.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке Outbox в Payments Service");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}