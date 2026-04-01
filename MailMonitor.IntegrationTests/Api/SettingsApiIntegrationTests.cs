using System.Net;
using System.Net.Http.Json;
using MailMonitor.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailMonitor.IntegrationTests.Api;

public sealed class SettingsApiIntegrationTests
{
    [Fact]
    public async Task Get_ShouldReturnSettingsPayload_WithRequiredFields()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/api/settings");
        var payload = await response.Content.ReadFromJsonAsync<SettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.BaseStorageFolder));
        Assert.NotNull(payload.MailSubjectKeywords);
        Assert.False(string.IsNullOrWhiteSpace(payload.ProcessingTag));
    }

    [Fact]
    public async Task Put_ShouldPersistBaseStorageFolder_InSqlite()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);
        var newBaseStorageFolder = @"C:\Pilot\Storage";

        var putResponse = await client.PutAsJsonAsync(
            "/api/settings",
            new { baseStorageFolder = newBaseStorageFolder });
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/settings");
        var payload = await getResponse.Content.ReadFromJsonAsync<SettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(newBaseStorageFolder, payload.BaseStorageFolder);
    }

    [Fact]
    public async Task Put_ShouldPersistProcessingActionsEnabledFlag()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        var putResponse = await client.PutAsJsonAsync(
            "/api/settings",
            new
            {
                baseStorageFolder = @"C:\Pilot\Storage",
                processingActionsEnabled = false
            });

        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/settings");
        var payload = await getResponse.Content.ReadFromJsonAsync<SettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload.ProcessingActionsEnabled);
    }

    private static HttpClient CreateClient(ApiTestFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed record SettingsResponse(
        string BaseStorageFolder,
        IReadOnlyCollection<string> MailSubjectKeywords,
        string ProcessingTag,
        bool ProcessingActionsEnabled);
}
