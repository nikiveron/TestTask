namespace Server.Infrastructure.RabbitMQ;

public sealed record TaskCompletedEvent(
Guid TaskId,
string Title,
DateTimeOffset CompletedAt,
string Priority);
