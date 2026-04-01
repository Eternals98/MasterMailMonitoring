using System.Net;
using System.Net.Http.Json;
using MailMonitor.Domain.Entities.Reporting;
using MailMonitor.IntegrationTests.Infrastructure;
using MailMonitor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailMonitor.IntegrationTests.Api;

public sealed class EmailStatisticsApiIntegrationTests
{
    [Fact]
    public async Task Get_ShouldReturnDataFilteredByCompanyProcessedAndDateRange()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        var dbPath = factory.GetResolvedConfigurationDbPath();
        var seed = SeedStatistics(dbPath);

        var from = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        var response = await client.GetAsync(
            $"/api/email-statistics?company={Uri.EscapeDataString(seed.CompanyName)}" +
            $"&processed=true&from={Uri.EscapeDataString(from.ToString("O"))}" +
            $"&to={Uri.EscapeDataString(to.ToString("O"))}");

        var payload = await response.Content.ReadFromJsonAsync<List<EmailStatisticsItemResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Single(payload);
        Assert.Equal(seed.ExpectedMessageId, payload[0].MessageId);
        Assert.All(payload, item =>
        {
            Assert.Equal(seed.CompanyName, item.Company);
            Assert.True(item.Processed);
            Assert.True(item.Date >= from);
            Assert.True(item.Date <= to);
        });
    }

    [Fact]
    public async Task Get_ShouldReturnBadRequest_WhenToIsBeforeFrom()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);

        var from = new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.GetAsync(
            $"/api/email-statistics?from={Uri.EscapeDataString(from.ToString("O"))}" +
            $"&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient CreateClient(ApiTestFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private static SeedResult SeedStatistics(string dbPath)
    {
        using var context = new ConfigurationDbContext(dbPath);
        context.Database.EnsureCreated();
        context.EnsureEmailStatisticsSchema();

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var companyName = $"Contoso-IT-{uniqueSuffix}";
        var expectedMessageId = $"contoso-in-range-{uniqueSuffix}";

        var inRange = new EmailProcessStatistic
        {
            Date = new DateTime(2026, 3, 24, 10, 0, 0, DateTimeKind.Utc),
            CompanyName = companyName,
            UserMail = "mail@contoso.com",
            Processed = true,
            Subject = "Invoice 001",
            AttachmentsCount = 2,
            Mailbox = "Inbox",
            StorageFolder = @"C:\Storage\Contoso",
            StoredAttachments = [@"C:\Storage\Contoso\invoice001.pdf"],
            MessageId = expectedMessageId
        };

        var outOfRange = new EmailProcessStatistic
        {
            Date = new DateTime(2026, 2, 22, 9, 0, 0, DateTimeKind.Utc),
            CompanyName = companyName,
            UserMail = "mail@contoso.com",
            Processed = true,
            Subject = "Invoice 002",
            AttachmentsCount = 1,
            Mailbox = "Inbox",
            StorageFolder = @"C:\Storage\Contoso",
            StoredAttachments = [@"C:\Storage\Contoso\invoice002.xml"],
            MessageId = $"contoso-out-range-{uniqueSuffix}"
        };

        var ignored = new EmailProcessStatistic
        {
            Date = new DateTime(2026, 3, 24, 10, 0, 0, DateTimeKind.Utc),
            CompanyName = companyName,
            UserMail = "mail@contoso.com",
            Processed = false,
            Subject = "Notification",
            AttachmentsCount = 0,
            ReasonIgnored = "Subject does not match global keywords",
            Mailbox = "Inbox",
            StorageFolder = @"C:\Storage\Contoso",
            StoredAttachments = Array.Empty<string>(),
            MessageId = $"contoso-ignored-{uniqueSuffix}"
        };

        context.EmailStatistics.AddRange(
            inRange,
            outOfRange,
            ignored);

        context.SaveChanges();

        return new SeedResult(companyName, expectedMessageId);
    }

    private sealed record EmailStatisticsItemResponse(
        Guid Id,
        DateTime Date,
        string Company,
        string UserMail,
        bool Processed,
        string Subject,
        int AttachmentsCount,
        string ReasonIgnored,
        string Mailbox,
        string StorageFolder,
        IReadOnlyCollection<string> StoredAttachments,
        string? MessageId);

    private sealed record SeedResult(string CompanyName, string ExpectedMessageId);
}
