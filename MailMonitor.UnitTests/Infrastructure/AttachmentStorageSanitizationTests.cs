using System.Text.RegularExpressions;
using MailMonitor.Application.Abstractions.Storage;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Models;

namespace MailMonitor.UnitTests.Infrastructure;

public sealed class AttachmentStorageSanitizationTests
{
    [Fact]
    public void StoreFile_ShouldSanitizeSubjectAndFileName_AndKeepPathUnderBaseFolder()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"mailmonitor-storage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        var dbPath = Path.Combine(rootPath, "config.db");
        using var serviceProvider = BuildServiceProvider(dbPath);

        try
        {
            var storageService = serviceProvider.GetRequiredService<IAttachmentStorageService>();
            var company = new Company
            {
                Name = "Contoso",
                Mail = "mail@contoso.com",
                StorageFolder = @"..\..\Client:North\Sub\..\Final",
                ReportOutputFolder = "Reports",
                ProcessingTag = "ONBASE"
            };

            var settings = new Setting { BaseStorageFolder = rootPath, ProcessingTag = "ONBASE" };
            var attachment = new FileAttachment
            {
                Name = @"..\..\Invoice<2026>:March.Pdf???",
                ContentBytes = [0x01, 0x02, 0x03]
            };

            var result = storageService.StoreFile(company, @"INV: 2026/03 \ North..Region", attachment, settings);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(result.Value.FilePath));

            var fullBasePath = Path.GetFullPath(rootPath);
            var fullStoragePath = Path.GetFullPath(result.Value.StoragePath);
            var fullFilePath = Path.GetFullPath(result.Value.FilePath);

            Assert.StartsWith(fullBasePath, fullStoragePath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(fullBasePath, fullFilePath, StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain("..", fullStoragePath, StringComparison.Ordinal);
            Assert.DoesNotContain("..", fullFilePath, StringComparison.Ordinal);

            var fileName = Path.GetFileName(fullFilePath);
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                Assert.DoesNotContain(invalidChar, fileName);
            }

            Assert.Matches(new Regex("_[0-9a-f]{6}(\\.[A-Za-z0-9]{1,12})?$"), fileName);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, true);
            }
        }
    }

    [Fact]
    public void StoreFile_ShouldUseNoSubjectDirectory_WhenSubjectIsEmpty()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"mailmonitor-storage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        var dbPath = Path.Combine(rootPath, "config.db");
        using var serviceProvider = BuildServiceProvider(dbPath);

        try
        {
            var storageService = serviceProvider.GetRequiredService<IAttachmentStorageService>();
            var company = new Company
            {
                Name = "Fabrikam",
                Mail = "mail@fabrikam.com",
                StorageFolder = "Fabrikam",
                ReportOutputFolder = "Reports",
                ProcessingTag = "ONBASE"
            };

            var settings = new Setting { BaseStorageFolder = rootPath, ProcessingTag = "ONBASE" };
            var attachment = new FileAttachment
            {
                Name = "invoice.pdf",
                ContentBytes = [0x11, 0x22, 0x33]
            };

            var result = storageService.StoreFile(company, "   ", attachment, settings);

            Assert.True(result.IsSuccess);
            Assert.Equal("no_subject", new DirectoryInfo(result.Value.StoragePath).Name);
            Assert.True(File.Exists(result.Value.FilePath));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, true);
            }
        }
    }

    [Fact]
    public void StoreFile_ShouldUseCompanyAbsoluteStorage_WhenOverrideGlobalStorageFolderIsEnabled()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"mailmonitor-storage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        var dbPath = Path.Combine(rootPath, "config.db");
        using var serviceProvider = BuildServiceProvider(dbPath);

        try
        {
            var globalBase = Path.Combine(rootPath, "global-base");
            var companyAbsolute = Path.Combine(rootPath, "company-absolute");

            var storageService = serviceProvider.GetRequiredService<IAttachmentStorageService>();
            var company = new Company
            {
                Name = "Northwind",
                Mail = "mail@northwind.com",
                StorageFolder = companyAbsolute,
                OverrideGlobalStorageFolder = true,
                ReportOutputFolder = "Reports",
                ProcessingTag = "ONBASE"
            };

            var settings = new Setting { BaseStorageFolder = globalBase, ProcessingTag = "ONBASE" };
            var attachment = new FileAttachment
            {
                Name = "invoice.pdf",
                ContentBytes = [0x11, 0x22, 0x33]
            };

            var result = storageService.StoreFile(company, "Invoice Subject", attachment, settings);

            Assert.True(result.IsSuccess);

            var fullGlobalBase = Path.GetFullPath(globalBase);
            var fullCompanyAbsolute = Path.GetFullPath(companyAbsolute);
            var fullStoragePath = Path.GetFullPath(result.Value.StoragePath);
            var fullFilePath = Path.GetFullPath(result.Value.FilePath);

            Assert.StartsWith(fullCompanyAbsolute, fullStoragePath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(fullCompanyAbsolute, fullFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.False(fullStoragePath.StartsWith(fullGlobalBase, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, true);
            }
        }
    }

    private static ServiceProvider BuildServiceProvider(string dbPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:ConfigurationDbPath"] = dbPath,
                ["Storage:MaxRetries"] = "1",
                ["Storage:BaseDelayMilliseconds"] = "10",
                ["Storage:MaxDelayMilliseconds"] = "10",
                ["Graph:Instance"] = "https://login.microsoftonline.com/",
                ["Graph:ClientId"] = "client-id",
                ["Graph:TenantId"] = "tenant-id",
                ["Graph:ClientSecret"] = "secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
