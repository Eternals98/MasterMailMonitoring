using System.Net;
using System.Net.Http.Json;
using MailMonitor.Domain.Entities.Reporting;
using MailMonitor.IntegrationTests.Infrastructure;
using MailMonitor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace MailMonitor.IntegrationTests.Api;

public sealed class EmailStatisticsApiIntegrationTests
{
    [Fact]
    public async Task Get_ShouldReturnDataFilteredByCompanyProcessedAndDateRange()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);
        SeedStatistics(factory);

        var from = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 30, 23, 59, 59, DateTimeKind.Utc);

        var response = await client.GetAsync(
            $"/api/email-statistics?company={Uri.EscapeDataString("Contoso")}" +
            $"&processed=true&from={Uri.EscapeDataString(from.ToString("O"))}" +
            $"&to={Uri.EscapeDataString(to.ToString("O"))}");

        var payload = await response.Content.ReadFromJsonAsync<List<EmailStatisticsItemResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Single(payload);
        Assert.All(payload, item =>
        {
            Assert.Equal("Contoso", item.Company);
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

    private static void SeedStatistics(ApiTestFactory factory)
    {
        using var context = new ConfigurationDbContext(factory.ConfigurationDbPath);
        context.Database.EnsureCreated();
        context.EnsureEmailStatisticsSchema();

        context.EmailStatistics.AddRange(
            new EmailProcessStatistic
            {
                Date = new DateTime(2026, 3, 24, 10, 0, 0, DateTimeKind.Utc),
                CompanyName = "Contoso",
                UserMail = "mail@contoso.com",
                Processed = true,
                Subject = "Invoice 001",
                AttachmentsCount = 2,
                Mailbox = "Inbox",
                StorageFolder = @"C:\Storage\Contoso",
                StoredAttachments = [@"C:\Storage\Contoso\invoice001.pdf"],
                MessageId = "contoso-001"
            },
            new EmailProcessStatistic
            {
                Date = new DateTime(2026, 2, 22, 9, 0, 0, DateTimeKind.Utc),
                CompanyName = "Contoso",
                UserMail = "mail@contoso.com",
                Processed = true,
                Subject = "Invoice 002",
                AttachmentsCount = 1,
                Mailbox = "Inbox",
                StorageFolder = @"C:\Storage\Contoso",
                StoredAttachments = [@"C:\Storage\Contoso\invoice002.xml"],
                MessageId = "contoso-002"
            },
            new EmailProcessStatistic
            {
                Date = new DateTime(2026, 3, 18, 9, 0, 0, DateTimeKind.Utc),
                CompanyName = "Fabrikam",
                UserMail = "mail@fabrikam.com",
                Processed = false,
                Subject = "Notification",
                AttachmentsCount = 0,
                ReasonIgnored = "Subject does not match global keywords",
                Mailbox = "Inbox",
                StorageFolder = @"C:\Storage\Fabrikam",
                StoredAttachments = Array.Empty<string>(),
                MessageId = "fabrikam-001"
            });

        context.SaveChanges();
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
}
