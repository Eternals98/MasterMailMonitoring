using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;

namespace MailMonitor.UnitTests.Domain;

public sealed class CompanyValidationTests
{
    [Fact]
    public void CreateValidated_ShouldFail_WhenNameIsEmpty()
    {
        var result = Company.CreateValidated(
            string.Empty,
            "mail@contoso.com",
            "2026-03-20T00:00:00Z",
            ["Inbox"],
            ["PDF"],
            ["invoice"],
            "CompanyA",
            "Reports",
            "ONBASE");

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.NameRequired.Code, result.Error.Code);
    }

    [Fact]
    public void CreateValidated_ShouldFail_WhenStartFromIsInvalid()
    {
        var result = Company.CreateValidated(
            "Contoso",
            "mail@contoso.com",
            "not-a-date",
            ["Inbox"],
            ["PDF"],
            ["invoice"],
            "CompanyA",
            "Reports",
            "ONBASE");

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.InvalidStartFrom.Code, result.Error.Code);
    }

    [Fact]
    public void CreateValidated_ShouldNormalizeListsAndTrimFields()
    {
        var result = Company.CreateValidated(
            "  Contoso  ",
            "  mail@contoso.com  ",
            " 2026-03-20T00:00:00Z ",
            [" Inbox ", "inbox", "Archive"],
            [" PDF ", "pdf", "XML "],
            [" invoice ", "Invoice", "xml"],
            "  Storage  ",
            "  Reports  ",
            "  ONBASE  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Contoso", result.Value.Name);
        Assert.Equal("mail@contoso.com", result.Value.Mail);
        Assert.Equal("2026-03-20T00:00:00Z", result.Value.StartFrom);
        Assert.Equal(["Inbox", "Archive"], result.Value.MailBox);
        Assert.Equal(["PDF", "XML"], result.Value.FileTypes);
        Assert.Equal(["invoice", "xml"], result.Value.AttachmentKeywords);
        Assert.Equal("Storage", result.Value.StorageFolder);
        Assert.Equal("Reports", result.Value.ReportOutputFolder);
        Assert.Equal("ONBASE", result.Value.ProcessingTag);
    }

    [Fact]
    public void CreateValidated_ShouldFail_WhenStorageIsAbsolute_AndGlobalStorageIsEnabled()
    {
        var result = Company.CreateValidated(
            "Contoso",
            "mail@contoso.com",
            "2026-03-20T00:00:00Z",
            ["Inbox"],
            ["PDF"],
            [],
            @"C:\CompanyA",
            "Reports",
            "ONBASE",
            true,
            false);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.StorageFolderMustBeRelativeWhenUsingGlobal.Code, result.Error.Code);
    }

    [Fact]
    public void CreateValidated_ShouldFail_WhenStorageIsRelative_AndGlobalStorageIsOverridden()
    {
        var result = Company.CreateValidated(
            "Contoso",
            "mail@contoso.com",
            "2026-03-20T00:00:00Z",
            ["Inbox"],
            ["PDF"],
            [],
            @"CompanyA\Inbox",
            "Reports",
            "ONBASE",
            true,
            true);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.StorageFolderMustBeAbsoluteWhenOverridingGlobal.Code, result.Error.Code);
    }

    [Fact]
    public void CreateValidated_ShouldSucceed_WhenStorageIsAbsolute_AndGlobalStorageIsOverridden()
    {
        var result = Company.CreateValidated(
            "Contoso",
            "mail@contoso.com",
            "2026-03-20T00:00:00Z",
            ["Inbox"],
            ["PDF"],
            [],
            @"\\server\share\contoso",
            "Reports",
            "ONBASE",
            true,
            true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OverrideGlobalStorageFolder);
    }

    [Fact]
    public void CreateValidated_ShouldFail_WhenReportOutputIsAbsolute_AndGlobalReportOutputIsEnabled()
    {
        var result = Company.CreateValidated(
            "Contoso",
            "mail@contoso.com",
            "2026-03-20T00:00:00Z",
            ["Inbox"],
            ["PDF"],
            [],
            @"CompanyA\Inbox",
            @"\\server\reports\contoso",
            "ONBASE",
            true,
            false,
            false);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.ReportOutputFolderMustBeRelativeWhenUsingGlobal.Code, result.Error.Code);
    }

    [Fact]
    public void CreateValidated_ShouldFail_WhenReportOutputIsRelative_AndGlobalReportOutputIsOverridden()
    {
        var result = Company.CreateValidated(
            "Contoso",
            "mail@contoso.com",
            "2026-03-20T00:00:00Z",
            ["Inbox"],
            ["PDF"],
            [],
            @"CompanyA\Inbox",
            @"Reports\Contoso",
            "ONBASE",
            true,
            false,
            true);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.ReportOutputFolderMustBeAbsoluteWhenOverridingGlobal.Code, result.Error.Code);
    }

    [Fact]
    public void CreateValidated_ShouldSucceed_WhenReportOutputIsAbsolute_AndGlobalReportOutputIsOverridden()
    {
        var result = Company.CreateValidated(
            "Contoso",
            "mail@contoso.com",
            "2026-03-20T00:00:00Z",
            ["Inbox"],
            ["PDF"],
            [],
            @"CompanyA\Inbox",
            @"D:\reports\contoso",
            "ONBASE",
            true,
            false,
            true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OverrideGlobalReportOutputFolder);
    }

    [Fact]
    public void RegisterProcessedEmail_ShouldFail_WhenSubjectIsEmpty()
    {
        var company = new Company();

        var result = company.RegisterProcessedEmail(" ", DateTime.UtcNow, 1);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.ProcessedSubjectRequired.Code, result.Error.Code);
    }

    [Fact]
    public void RegisterProcessedEmail_ShouldFail_WhenAttachmentsCountIsNegative()
    {
        var company = new Company();

        var result = company.RegisterProcessedEmail("Invoice", DateTime.UtcNow, -1);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrors.Company.InvalidProcessedAttachmentCount.Code, result.Error.Code);
    }

    [Fact]
    public void RegisterProcessedEmail_ShouldPersistProcessedMetadata()
    {
        var company = new Company();
        var processedAt = new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);

        var result = company.RegisterProcessedEmail("  Invoice 2026 ", processedAt, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(Company.RecordTypeProcessedEmail, company.RecordType);
        Assert.Equal("Invoice 2026", company.ProcessedSubject);
        Assert.Equal(processedAt, company.ProcessedDate);
        Assert.Equal(2, company.ProcessedAttachmentsCount);
    }
}
