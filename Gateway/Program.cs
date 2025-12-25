var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку прокси, читая секцию из appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Включаем маршрутизацию прокси
app.MapReverseProxy();

app.Run();