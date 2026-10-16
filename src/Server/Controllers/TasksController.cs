using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Application.CreateTask;
using Server.Domain;
using Server.Infrastructure;
using Server.Infrastructure.RabbitMQ;

namespace Server.Controllers;

[Route("tasks")]
[ApiController]
public class TasksController(AppDbContext appDbContext, ITaskEventPublisher taskEventPublisher) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetTasksList(CancellationToken cancellationToken)
    {
        var tasks = await appDbContext.TaskItems
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync(cancellationToken);

        return Ok(tasks);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            return BadRequest(new
            {
                error = "Название должно содержать 1–200 символов"
            });
        }

        var task = new TaskItem
        {
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            Priority = Priority.Medium,
            IsCompleted = false,
            CompletedAt = null
        };

        appDbContext.TaskItems.Add(task);
        await appDbContext.SaveChangesAsync(cancellationToken);

        return Created();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        var task = await appDbContext.TaskItems.FindAsync([id], cancellationToken);

        if (task is null)
            return NotFound();

        appDbContext.TaskItems.Remove(task);
        await appDbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPut("{id:guid}/complete")]
    public async Task<IActionResult> UpdateTask(Guid id, CancellationToken cancellationToken)
    {
        var task = await appDbContext.TaskItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (task is null)
            return NotFound();

        if (task.IsCompleted)
            return BadRequest(new
            {
                error = "Задача уже завершена."
            });

        task.IsCompleted = true;
        task.CompletedAt = DateTimeOffset.UtcNow;
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                error = "Задача была изменена другим пользователем. Пожалуйста, обновите страницу и попробуйте снова."
            });
        }

        var message = new TaskCompletedEvent(task.Id, task.Title, task.CompletedAt.Value, task.Priority.ToString());

        await taskEventPublisher.PublishCompletedAsync(message, cancellationToken);

        return NoContent();
    }
}
