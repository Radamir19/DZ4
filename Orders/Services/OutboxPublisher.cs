using System.Text;
using Orders.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace Orders.Services;

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
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                // В v7.0+ настройки по умолчанию уже асинхронные
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Объявляем очередь, в которую будем слать сообщения о создании заказа
            await _channel.QueueDeclareAsync(
                queue: "order_created", 
                durable: true, 
                exclusive: false, 
                autoDelete: false, 
                cancellationToken: stoppingToken);
            
            _logger.LogInformation("OutboxPublisher успешно подключен к RabbitMQ.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось инициализировать RabbitMQ. Попробуем позже.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Инициализируем соединение перед входом в цикл
        await InitializeRabbitMQAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Если канал упал, пробуем переподключиться
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
                    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

                    // 1. Извлекаем пачку необработанных сообщений
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

                            // 2. Публикуем сообщение в RabbitMQ
                            await _channel.BasicPublishAsync(
                                exchange: string.Empty,
                                routingKey: "order_created",
                                body: body,
                                cancellationToken: stoppingToken);

                            // 3. Помечаем как обработанное
                            message.IsProcessed = true;
                            _logger.LogInformation("Сообщение Outbox {MessageId} отправлено в RabbitMQ", message.Id);
                        }

                        // 4. Сохраняем изменения в БД одним махом
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке Outbox сообщений");
            }

            // Интервал опроса БД (например, каждые 2 секунды)
            await Task.Delay(2000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OutboxPublisher останавливается...");
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}