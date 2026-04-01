using System.Net;
using System.Net.Http.Json;
using MailMonitor.IntegrationTests.Infrastructure;
using MailMonitor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace MailMonitor.IntegrationTests.Api;

public sealed class GraphSettingsApiIntegrationTests
{
    [Fact]
    public async Task Get_ShouldMaskShortClientSecret_Completely()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        var putResponse = await client.PutAsJsonAsync(
            "/api/graph-settings",
            new
            {
                instance = "https://login.microsoftonline.com/",
                clientId = "short-secret-client",
                tenantId = "short-secret-tenant",
                clientSecret = "abc",
                graphUserScopesJson = "[\"https://graph.microsoft.com/.default\"]"
            });

        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/graph-settings");
        var payload = await getResponse.Content.ReadFromJsonAsync<GraphSettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("***", payload.ClientSecretMasked);
    }

    [Fact]
    public async Task Get_ShouldShowOnlyLastFourCharacters_ForLongClientSecret()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);
        const string longSecret = "supersecret42";

        var putResponse = await client.PutAsJsonAsync(
            "/api/graph-settings",
            new
            {
                instance = "https://login.microsoftonline.com/",
                clientId = "long-secret-client",
                tenantId = "long-secret-tenant",
                clientSecret = longSecret,
                graphUserScopesJson = "[\"https://graph.microsoft.com/.default\"]"
            });

        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/graph-settings");
        var payload = await getResponse.Content.ReadFromJsonAsync<GraphSettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("*********et42", payload.ClientSecretMasked);
    }

    [Fact]
    public async Task Put_ShouldKeepSingleGraphSettingsRow_InDatabase()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        await client.PutAsJsonAsync(
            "/api/graph-settings",
            new
            {
                instance = "https://login.microsoftonline.com/",
                clientId = "singleton-client-a",
                tenantId = "singleton-tenant-a",
                clientSecret = "secret-a",
                graphUserScopesJson = "[\"https://graph.microsoft.com/.default\"]"
            });

        await client.PutAsJsonAsync(
            "/api/graph-settings",
            new
            {
                instance = "https://login.microsoftonline.com/",
                clientId = "singleton-client-b",
                tenantId = "singleton-tenant-b",
                clientSecret = "secret-b",
                graphUserScopesJson = "[\"https://graph.microsoft.com/.default\"]"
            });

        using var context = new ConfigurationDbContext(factory.GetResolvedConfigurationDbPath());
        var count = await context.GraphSettings.CountAsync();
        var singleton = await context.GraphSettings.SingleAsync();

        Assert.Equal(1, count);
        Assert.Equal(1, singleton.Id);
        Assert.Equal("singleton-client-b", singleton.ClientId);
    }

    [Fact]
    public async Task Verify_ShouldPersistLastVerificationResult()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        var verifyResponse = await client.PostAsJsonAsync("/api/graph-settings/verify", new { });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, verifyResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/graph-settings");
        var payload = await getResponse.Content.ReadFromJsonAsync<GraphSettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload.LastVerificationSucceeded ?? true);
        Assert.NotNull(payload.LastVerificationAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(payload.LastVerificationErrorCode));
    }

    private static HttpClient CreateClient(ApiTestFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed record GraphSettingsResponse(
        string Instance,
        string ClientId,
        string TenantId,
        string ClientSecretMasked,
        string GraphUserScopesJson,
        DateTime? LastVerificationAtUtc,
        bool? LastVerificationSucceeded,
        string LastVerificationErrorCode,
        string LastVerificationErrorMessage);
}
