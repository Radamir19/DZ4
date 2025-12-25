using Microsoft.EntityFrameworkCore;
using Orders.Data;
using Orders.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Настройка подключения к базе данных PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Регистрация контроллеров и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Регистрация бизнес-логики (Scoped)
builder.Services.AddScoped<IOrderService, OrderService>();

// 4. Регистрация фоновых задач (Singleton/Hosted)
// Важно: эти сервисы сами создают Scope для работы с БД внутри себя
builder.Services.AddHostedService<OutboxPublisherService>(); // Отправка в RabbitMQ
builder.Services.AddHostedService<RabbitMQConsumer>();        // Прием ответов из RabbitMQ

var app = builder.Build();

// 5. Автоматическое применение миграций при запуске (опционально, но удобно для ДЗ)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    // Это создаст базу и таблицы, если их еще нет
    // db.Database.Migrate(); 
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); // или PaymentsDbContext
    context.Database.EnsureCreated();
}
app.Run();