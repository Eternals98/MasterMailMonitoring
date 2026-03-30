using System.Net;
using System.Net.Http.Json;
using MailMonitor.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailMonitor.IntegrationTests.Api;

public sealed class TriggersApiIntegrationTests
{
    [Fact]
    public async Task Post_ShouldReturnCreatedAndAllowGetById()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/triggers",
            new
            {
                name = $"D5 Trigger {Guid.NewGuid():N}",
                cronExpression = "0/30 * * ? * * *"
            });

        var created = await response.Content.ReadFromJsonAsync<TriggerResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Contains($"/api/triggers/{created.Id}", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        var getById = await client.GetAsync($"/api/triggers/{created.Id}");
        var fetched = await getById.Content.ReadFromJsonAsync<TriggerResponse>();

        Assert.Equal(HttpStatusCode.OK, getById.StatusCode);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Name, fetched.Name);
        Assert.Equal(created.CronExpression, fetched.CronExpression);
    }

    private static HttpClient CreateClient(ApiTestFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed record TriggerResponse(
        Guid Id,
        string Name,
        string CronExpression);
}
