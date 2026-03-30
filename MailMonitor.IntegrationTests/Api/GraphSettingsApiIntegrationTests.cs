using System.Net;
using System.Net.Http.Json;
using MailMonitor.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

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
        string GraphUserScopesJson);
}
