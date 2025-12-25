using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Services;

public class RabbitMQConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private IConnection? _connection;
    private IChannel? _channel; // В v7 вместо IModel используется IChannel

    public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Настройка фабрики (в v7 DispatchConsumersAsync больше не нужен)
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMQ:Host"] ?? "localhost"
        };

        // 2. Асинхронное создание соединения и канала
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // 3. Объявление очереди
        await _channel.QueueDeclareAsync(
            queue: "payment_results_queue", 
            durable: true, 
            exclusive: false, 
            autoDelete: false, 
            cancellationToken: stoppingToken);

        // 4. Настройка потребителя (Consumer)
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                using var jsonDoc = JsonDocument.Parse(message);
                var orderId = jsonDoc.RootElement.GetProperty("OrderId").GetGuid();
                var success = jsonDoc.RootElement.GetProperty("Success").GetBoolean();

                using (var scope = _serviceProvider.CreateScope())
                {
                    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                    string newStatus = success ? "FINISHED" : "CANCELLED";
                    await orderService.UpdateOrderStatusAsync(orderId, newStatus);
                }

                // Подтверждение получения (Ack)
                await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                // В случае ошибки логируем и можно сделать Nack (отклонение)
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        // 5. Запуск прослушивания
        await _channel.BasicConsumeAsync(
            queue: "payment_results_queue", 
            autoAck: false, 
            consumer: consumer, 
            cancellationToken: stoppingToken);

        // Ждем, пока сервис не будет остановлен
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}