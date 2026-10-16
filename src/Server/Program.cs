using Microsoft.EntityFrameworkCore;
using Server.Infrastructure;
using Server.Infrastructure.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

services.AddSingleton<ITaskEventPublisher, RabbitMqService>();

services.AddControllers();
services.AddOpenApi();
services.AddSwaggerGen();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                                  .CreateLogger("Startup");
try
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Миграция успешно применена к базе данных");
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Не удалось применить миграцию к базе данных");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInformation("Приложение останавливается, закрываю соединения...");
    var taskEventPublisher = app.Services.GetService<ITaskEventPublisher>();
    taskEventPublisher?.Dispose();
});

app.Run();

public partial class Program { }