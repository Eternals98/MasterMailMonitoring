using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Reporting;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Domain.Repositories;
using MailMonitor.Worker.Services;
using Microsoft.Graph.Models;

namespace MailMonitor.UnitTests.Worker;

public sealed class MessageFilterServiceTests
{
    [Fact]
    public void EvaluateMessage_ShouldSkip_WhenSubjectDoesNotMatchConfiguredKeywords()
    {
        var service = new MessageFilterService(new FakeEmailStatisticsRepository());
        var company = BuildCompany();
        var settings = new Setting
        {
            ProcessingTag = "ONBASE",
            MailSubjectKeywords = "invoice;credit-note|tax"
        };

        var message = new Message
        {
            Subject = "Weekly newsletter",
            HasAttachments = true
        };

        var result = service.EvaluateMessage(company, settings, message, "msg-001");

        Assert.True(result.ShouldSkip);
        Assert.Equal("Subject does not match global keywords", result.Reason);
    }

    [Fact]
    public void EvaluateMessage_ShouldContinue_WhenSubjectMatchesKeywordWithMixedSeparators()
    {
        var service = new MessageFilterService(new FakeEmailStatisticsRepository());
        var company = BuildCompany();
        var settings = new Setting
        {
            ProcessingTag = "ONBASE",
            MailSubjectKeywords = " invoice ; TAX | invoice "
        };

        var message = new Message
        {
            Subject = "Tax report March 2026",
            HasAttachments = true
        };

        var result = service.EvaluateMessage(company, settings, message, "msg-002");

        Assert.False(result.ShouldSkip);
        Assert.Equal("Tax report March 2026", result.Subject);
    }

    [Fact]
    public void EvaluateMessage_ShouldSkip_WhenMessageIdWasAlreadyProcessed()
    {
        var service = new MessageFilterService(new FakeEmailStatisticsRepository(isProcessed: true));
        var company = BuildCompany();
        var settings = new Setting
        {
            ProcessingTag = "ONBASE",
            MailSubjectKeywords = "invoice"
        };

        var message = new Message
        {
            Subject = "Invoice 301",
            HasAttachments = true
        };

        var result = service.EvaluateMessage(company, settings, message, "msg-003");

        Assert.True(result.ShouldSkip);
        Assert.Contains("already processed previously", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateMessage_ShouldUseGlobalTag_WhenCompanyOverrideIsDisabled()
    {
        var service = new MessageFilterService(new FakeEmailStatisticsRepository());
        var company = BuildCompany();
        company.ProcessingTag = "COMPANY-TAG";
        company.OverrideGlobalProcessingTag = false;

        var settings = new Setting
        {
            ProcessingTag = "GLOBAL-TAG",
            MailSubjectKeywords = "invoice"
        };

        var message = new Message
        {
            Subject = "Invoice 998",
            HasAttachments = true
        };

        var result = service.EvaluateMessage(company, settings, message, "msg-override-001");

        Assert.False(result.ShouldSkip);
        Assert.Equal("GLOBAL-TAG", result.ProcessingTag);
    }

    [Fact]
    public void FilterAttachments_ShouldApplyExtensionAndKeywordFilters()
    {
        var service = new MessageFilterService(new FakeEmailStatisticsRepository());
        var company = new Company
        {
            FileTypes = ["pdf", "xml"],
            AttachmentKeywords = ["invoice", "tax"]
        };

        var attachments = new[]
        {
            new FileAttachment { Name = "invoice-001.PDF" },
            new FileAttachment { Name = "tax-retention.xml" },
            new FileAttachment { Name = "manual.pdf" },
            new FileAttachment { Name = "invoice-001.txt" },
            new FileAttachment { Name = string.Empty }
        };

        var filtered = service.FilterAttachments(company, attachments);

        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, item => item.Name == "invoice-001.PDF");
        Assert.Contains(filtered, item => item.Name == "tax-retention.xml");
    }

    [Fact]
    public void FilterAttachments_ShouldOnlyApplyKeywordFilter_WhenNoExtensionsConfigured()
    {
        var service = new MessageFilterService(new FakeEmailStatisticsRepository());
        var company = new Company
        {
            FileTypes = [],
            AttachmentKeywords = ["invoice"]
        };

        var attachments = new[]
        {
            new FileAttachment { Name = "invoice.docx" },
            new FileAttachment { Name = "monthly.pdf" }
        };

        var filtered = service.FilterAttachments(company, attachments);

        Assert.Single(filtered);
        Assert.Equal("invoice.docx", filtered.First().Name);
    }

    private static Company BuildCompany()
    {
        return new Company
        {
            Name = "Contoso",
            Mail = "mail@contoso.com",
            StorageFolder = "Contoso",
            ReportOutputFolder = "Reports",
            ProcessingTag = "ONBASE"
        };
    }

    private sealed class FakeEmailStatisticsRepository : IEmailStatisticsRepository
    {
        private readonly bool _isProcessed;

        public FakeEmailStatisticsRepository(bool isProcessed = false)
        {
            _isProcessed = isProcessed;
        }

        public void LogEmailProcess(EmailProcessStatistic statistic)
        {
        }

        public IEnumerable<EmailProcessStatistic> GetEmailStatistics()
        {
            return [];
        }

        public bool HasProcessedMessage(string messageId)
        {
            return _isProcessed;
        }
    }
}
