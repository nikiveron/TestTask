using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Server.Infrastructure.RabbitMQ;

public class RabbitMqService(IConfiguration config, ILogger<RabbitMqService> logger) : ITaskEventPublisher
{
    private IConnection? _connection;
    private bool _disposed;
    private readonly IConfiguration _config = config;
    private readonly ILogger<RabbitMqService> _logger = logger;
    private readonly string _exchangeName = "task.events";
    private readonly string _routingKey = "task.completed";

    public Task PublishCompletedAsync(TaskCompletedEvent message, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Publish(message), cancellationToken);
    }

    private void Publish(TaskCompletedEvent message)
    {
        try
        {
            var connection = GetOrCreateConnection();
            if (connection == null) return;

            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(_exchangeName, ExchangeType.Direct, durable: true);

            var json = JsonSerializer.Serialize(new
            {
                message.TaskId,
                message.Title,
                message.CompletedAt,
                message.Priority
            });

            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            channel.BasicPublish(_exchangeName, _routingKey, props, Encoding.UTF8.GetBytes(json));

            _logger.LogInformation("Событие завершения задачи опубликовано: {TaskId}", message.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при публикации события задачи: {TaskId}", message.TaskId);
        }
    }

    private IConnection? GetOrCreateConnection()
    {
        if (_connection?.IsOpen == true)
            return _connection;

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_config.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672/"),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _logger.LogInformation("Подключение к RabbitMQ установлено");
            return _connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось подключиться к RabbitMQ");
            _connection = null;
            return null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_connection?.IsOpen == true)
            {
                try
                {
                    _connection.Close(TimeSpan.FromSeconds(5));
                    _logger.LogInformation("Подключение к RabbitMQ закрыто корректно");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при закрытии подключения к RabbitMQ");
                }
            }
            _connection?.Dispose();
        }

        _disposed = true;
    }

    ~RabbitMqService()
    {
        Dispose(false);
    }
}
