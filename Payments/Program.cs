using Microsoft.EntityFrameworkCore;
using Payments.Data;
using Payments.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. База данных
builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Инфраструктура
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Бизнес-логика
builder.Services.AddScoped<IPaymentService, PaymentService>();

// 4. Фоновые воркеры
builder.Services.AddHostedService<OrderCreatedConsumer>();    // Слушает заказы
builder.Services.AddHostedService<OutboxPublisherService>();   // Шлет ответы

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>(); 
    context.Database.EnsureCreated();
}
app.Run();