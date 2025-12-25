using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Payments.Services;

public class OrderCreatedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public OrderCreatedConsumer(
        IServiceProvider serviceProvider, 
        IConfiguration config, 
        ILogger<OrderCreatedConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Настройка подключения
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "localhost"
        };

        try
        {
            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // 2. Объявляем очередь (должна совпадать с той, куда шлет Orders)
            await _channel.QueueDeclareAsync(
                queue: "order_created", 
                durable: true, 
                exclusive: false, 
                autoDelete: false, 
                cancellationToken: stoppingToken);

            _logger.LogInformation("Ожидание сообщений о новых заказах (очередь order_created)...");

            // 3. Настройка асинхронного потребителя
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Получено событие создания заказа: {Message}", message);

                try
                {
                    // Десериализуем данные (OrderId, UserId, Amount)
                    using var jsonDoc = JsonDocument.Parse(message);
                    var root = jsonDoc.RootElement;
                    
                    var orderId = root.GetProperty("Id").GetGuid();
                    var userId = root.GetProperty("UserId").GetString() ?? "";
                    var amount = root.GetProperty("Amount").GetDecimal();

                    // Создаем Scope, чтобы получить Scoped сервис PaymentService
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                        
                        // Вызываем логику оплаты (внутри которой Inbox, Outbox и CAS)
                        await paymentService.ProcessOrderPaymentAsync(orderId, userId, amount);
                    }

                    // Подтверждаем успешную обработку
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обработке заказа в Payments Service");
                    
                    // В случае временной ошибки (например, БД недоступна) 
                    // можно вернуть сообщение в очередь (requeue: true)
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
            };

            // 4. Запуск прослушивания
            await _channel.BasicConsumeAsync(
                queue: "order_created", 
                autoAck: false, 
                consumer: consumer, 
                cancellationToken: stoppingToken);

            // Держим сервис запущенным
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Критическая ошибка RabbitMQ Consumer");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}