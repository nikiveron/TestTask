using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Server.Application.CreateTask;
using Server.Domain;
using Server.Infrastructure;
using Server.Infrastructure.RabbitMQ;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Tests;

public class CompleteTaskIntegrationTest : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _httpClient = null!;
    private Mock<ITaskEventPublisher> _mockPublisher = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var providerConfigurations = services
                    .Where(d => d.ServiceType ==
                        typeof(IDbContextOptionsConfiguration<AppDbContext>))
                    .ToList();

                    foreach (var descriptor in providerConfigurations)
                    {
                        services.Remove(descriptor);
                    }

                    var dbDescriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(AppDbContext) ||
                        d.ServiceType == typeof(DbContext) ||
                        d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                    .ToList();

                    foreach (var descriptor in dbDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase("TestDb"));

                    _mockPublisher = new Mock<ITaskEventPublisher>();
                    _mockPublisher
                        .Setup(x => x.PublishCompletedAsync(It.IsAny<TaskCompletedEvent>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

                    var publisherDescriptors = services
                    .Where(d => d.ServiceType == typeof(ITaskEventPublisher))
                    .ToList();

                    foreach (var descriptor in publisherDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton(_mockPublisher.Object);
                });
            });

        _httpClient = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        _httpClient.Dispose();
    }

    [Fact]
    public async Task CreateAndCompleteTask_ShouldPublishMessageToRabbitMQ()
    {
        // Arrange
        var createRequest = new CreateTaskRequest("Buy milk");

        var createResponse = await _httpClient.PostAsJsonAsync("/tasks", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var tasksResponse = await _httpClient.GetAsync("/tasks");
        tasksResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var tasksJson = await tasksResponse.Content.ReadAsStringAsync();
        var tasks = JsonSerializer.Deserialize<List<TaskItem>>(tasksJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        tasks.Should().HaveCount(1);
        var taskId = tasks![0].Id;
        var taskTitle = tasks[0].Title;

        taskTitle.Should().Be("Buy milk");
        tasks[0].IsCompleted.Should().BeFalse();
        tasks[0].CompletedAt.Should().BeNull();

        // Act
        var completeResponse = await _httpClient.PutAsync($"/tasks/{taskId}/complete", null);

        // Assert
        completeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var updatedTasksResponse = await _httpClient.GetAsync("/tasks");
        var updatedTasksJson = await updatedTasksResponse.Content.ReadAsStringAsync();
        var updatedTasks = JsonSerializer.Deserialize<List<TaskItem>>(updatedTasksJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        updatedTasks.Should().HaveCount(1);
        updatedTasks![0].IsCompleted.Should().BeTrue();
        updatedTasks[0].CompletedAt.Should().NotBeNull();

        _mockPublisher.Verify(
            x => x.PublishCompletedAsync(
                It.Is<TaskCompletedEvent>(e =>
                    e.TaskId == taskId &&
                    e.Title == "Buy milk" &&
                    e.Priority == "Medium" &&
                    e.CompletedAt != DateTimeOffset.MinValue),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "RabbitMQ должен был получить сообщение о завершении задачи");
    }
}
