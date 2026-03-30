using MailMonitor.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MailMonitor.IntegrationTests.Infrastructure;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _testRootPath;

    public ApiTestFactory()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"mailmonitor-api-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRootPath);
        ConfigurationDbPath = Path.Combine(_testRootPath, "mailmonitor.integration.db");
    }

    public string ConfigurationDbPath { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:ConfigurationDbPath"] = ConfigurationDbPath,
                ["Storage:MaxRetries"] = "1",
                ["Storage:BaseDelayMilliseconds"] = "10",
                ["Storage:MaxDelayMilliseconds"] = "10",
                ["Graph:Instance"] = "https://login.microsoftonline.com/",
                ["Graph:ClientId"] = "integration-client-id",
                ["Graph:TenantId"] = "integration-tenant-id",
                ["Graph:ClientSecret"] = "integration-secret",
                ["Graph:Scopes:0"] = "https://graph.microsoft.com/.default"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        if (Directory.Exists(_testRootPath))
        {
            TryDeleteDirectory(_testRootPath);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }

                return;
            }
            catch (IOException)
            {
                if (attempt >= maxAttempts)
                {
                    return;
                }

                Thread.Sleep(100 * attempt);
            }
            catch (UnauthorizedAccessException)
            {
                if (attempt >= maxAttempts)
                {
                    return;
                }

                Thread.Sleep(100 * attempt);
            }
        }
    }
}
