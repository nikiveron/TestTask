namespace Server.Domain;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; } 
    public Priority Priority { get; set; } = Priority.Medium;

    public uint Version { get; set; }
}
