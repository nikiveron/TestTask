namespace Server.Infrastructure.RabbitMQ;

public interface ITaskEventPublisher : IDisposable
{
    public Task PublishCompletedAsync(
        TaskCompletedEvent message,
        CancellationToken cancellationToken = default);
}
