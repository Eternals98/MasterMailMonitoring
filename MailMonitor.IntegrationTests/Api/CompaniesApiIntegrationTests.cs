using System.Net;
using System.Net.Http.Json;
using MailMonitor.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MailMonitor.IntegrationTests.Api;

public sealed class CompaniesApiIntegrationTests
{
    [Fact]
    public async Task Crud_ShouldPersistAndReturnCompany_FromSqlite()
    {
        using var factory = new ApiTestFactory();
        using var client = CreateClient(factory);
        var companyName = $"Pilot Co {Guid.NewGuid():N}";

        var createRequest = new
        {
            name = companyName,
            mail = "pilot@contoso.com",
            startFrom = "2026-03-25T00:00:00Z",
            mailBox = new[] { "Inbox" },
            fileTypes = new[] { "PDF", "XML" },
            attachmentKeywords = new[] { "invoice", "tax" },
            storageFolder = @"Companies\Contoso",
            reportOutputFolder = @"Reports\Contoso",
            processingTag = "ONBASE"
        };

        var createResponse = await client.PostAsJsonAsync("/api/companies", createRequest);
        var createdCompany = await createResponse.Content.ReadFromJsonAsync<CompanyDetailResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdCompany);
        Assert.Equal(companyName, createdCompany.Name);

        var getByIdResponse = await client.GetAsync($"/api/companies/{createdCompany.Id}");
        var getByIdPayload = await getByIdResponse.Content.ReadFromJsonAsync<CompanyDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
        Assert.NotNull(getByIdPayload);
        Assert.Equal(createdCompany.Id, getByIdPayload.Id);
        Assert.Equal("pilot@contoso.com", getByIdPayload.Mail);

        var getFilteredResponse = await client.GetAsync($"/api/companies?name={Uri.EscapeDataString(companyName)}");
        var filteredCompanies = await getFilteredResponse.Content.ReadFromJsonAsync<List<CompanyListItemResponse>>();

        Assert.Equal(HttpStatusCode.OK, getFilteredResponse.StatusCode);
        Assert.NotNull(filteredCompanies);
        Assert.Contains(filteredCompanies, item => item.Id == createdCompany.Id);

        var updateRequest = new
        {
            id = createdCompany.Id,
            name = $"{companyName} Updated",
            mail = "pilot-updated@contoso.com",
            startFrom = "2026-03-26T00:00:00Z",
            mailBox = new[] { "Inbox", "Archive" },
            fileTypes = new[] { "PDF" },
            attachmentKeywords = new[] { "invoice" },
            storageFolder = @"Companies\Contoso\Updated",
            reportOutputFolder = @"Reports\Contoso\Updated",
            processingTag = "ONBASE-PILOT"
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/companies/{createdCompany.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var getUpdatedResponse = await client.GetAsync($"/api/companies/{createdCompany.Id}");
        var updatedCompany = await getUpdatedResponse.Content.ReadFromJsonAsync<CompanyDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, getUpdatedResponse.StatusCode);
        Assert.NotNull(updatedCompany);
        Assert.Equal($"{companyName} Updated", updatedCompany.Name);
        Assert.Equal("pilot-updated@contoso.com", updatedCompany.Mail);
        Assert.Equal("ONBASE-PILOT", updatedCompany.ProcessingTag);

        var deleteResponse = await client.DeleteAsync($"/api/companies/{createdCompany.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getDeletedResponse = await client.GetAsync($"/api/companies/{createdCompany.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
    }

    private static HttpClient CreateClient(ApiTestFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed record CompanyDetailResponse(
        Guid Id,
        string Name,
        string Mail,
        string StartFrom,
        IReadOnlyCollection<string> MailBox,
        IReadOnlyCollection<string> FileTypes,
        IReadOnlyCollection<string> AttachmentKeywords,
        string StorageFolder,
        string ReportOutputFolder,
        string ProcessingTag,
        string RecordType,
        string ProcessedSubject,
        DateTime? ProcessedDate,
        int ProcessedAttachmentsCount);

    private sealed record CompanyListItemResponse(
        Guid Id,
        string Name,
        string Mail,
        string StartFrom,
        string StorageFolder,
        string ReportOutputFolder,
        string ProcessingTag);
}
