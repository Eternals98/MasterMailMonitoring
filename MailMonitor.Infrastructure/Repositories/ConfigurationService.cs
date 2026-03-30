using System.Data;
using System.Text.Json;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Graph;
using MailMonitor.Domain.Entities.Jobs;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Domain.Repositories;
using MailMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MailMonitor.Infrastructure.Configuration
{
    public sealed class ConfigurationService : IConfigurationService, IConfigurationRepository
    {
        private readonly string _dbPath;
        private readonly IConfiguration _configuration;

        public ConfigurationService(string dbPath, IConfiguration configuration)
        {
            _dbPath = dbPath;
            _configuration = configuration;

            using var context = new ConfigurationDbContext(_dbPath);
            context.Database.EnsureCreated();

            EnsureCompanySchema(context);
            context.EnsureEmailStatisticsSchema();

            SeedDefaultSettings(context);
            SeedDefaultCompany(context);
            SeedDefaultTrigger(context);
            SeedDefaultGraphSettings(context);
            RemoveLegacySeededGraphSecrets(context);
        }

        private ConfigurationDbContext CreateContext()
        {
            var context = new ConfigurationDbContext(_dbPath);
            context.EnsureEmailStatisticsSchema();
            return context;
        }

        private static void SeedDefaultSettings(ConfigurationDbContext context)
        {
            if (context.AppSettings.Any())
            {
                return;
            }

            context.AppSettings.AddRange(
                new AppSetting { Key = Setting.KeyBaseStorageFolder, Value = @"C:\\OnBase" },
                new AppSetting { Key = Setting.KeyMailSubjectKeywords, Value = "TestOnBase" },
                new AppSetting { Key = Setting.KeyProcessingTag, Value = Company.DefaultProcessingTag });

            context.SaveChanges();
        }

        private static void SeedDefaultCompany(ConfigurationDbContext context)
        {
            if (context.Companies.Any(company => company.RecordType == Company.RecordTypeSetting))
            {
                return;
            }

            var defaultCompany = new Company
            {
                Name = "Aceros Abonos Agro",
                Mail = "aaacontabilidadprueba@abonosagro.com",
                StartFrom = "2025-08-13T00:00:00Z",
                MailBox = new List<string>
                {
                    "AAMkADI1NjQ0YWU0LTU2MmEtNDUxMS1iZGM3LWMyMTAwYzU4MmNhNwAuAAAAAABXZQQD1fDNRr2-S3QkRpLTAQBupptc987_QI6OTla8Kr7lAAQWV1SBAAA="
                },
                FileTypes = new List<string> { "PDF", "XML" },
                AttachmentKeywords = new List<string>(),
                StorageFolder = "\\Test",
                ReportOutputFolder = "\\Reports",
                ProcessingTag = Company.DefaultProcessingTag,
                RecordType = Company.RecordTypeSetting
            };

            context.Companies.Add(defaultCompany);
            context.SaveChanges();
        }

        private static void SeedDefaultTrigger(ConfigurationDbContext context)
        {
            if (context.Triggers.Any())
            {
                return;
            }

            var defaultTrigger = new Trigger
            {
                Name = "DefaultTrigger Each 10 min",
                CronExpression = "0 0/10 * ? * * *"
            };

            context.Triggers.Add(defaultTrigger);
            context.SaveChanges();
        }

        private void SeedDefaultGraphSettings(ConfigurationDbContext context)
        {
            if (context.GraphSettings.Any())
            {
                return;
            }

            var configuredScopes = ReadScopesFromConfiguration();
            var scopes = configuredScopes is { Length: > 0 }
                ? configuredScopes
                : new[] { "https://graph.microsoft.com/.default" };

            var defaultGraphSettings = new GraphSetting
            {
                Instance = _configuration["Graph:Instance"] ?? "https://login.microsoftonline.com/",
                ClientId = _configuration["Graph:ClientId"] ?? string.Empty,
                TenantId = _configuration["Graph:TenantId"] ?? string.Empty,
                ClientSecret = _configuration["Graph:ClientSecret"] ?? string.Empty,
                GraphUserScopesJson = JsonSerializer.Serialize(scopes)
            };

            context.GraphSettings.Add(defaultGraphSettings);
            context.SaveChanges();
        }

        private static void RemoveLegacySeededGraphSecrets(ConfigurationDbContext context)
        {
            const string legacyClientId = "0VuXLB0dfOCIRk9dkyZzuL9wS1VnzUd3AzetVr+rEiTBsJ+1aNP7oin+2jSzowEaOjg/IMy4Xk19ssxJi8eh1A==";
            const string legacyTenantId = "OsBnxBqaQqJafpT3oqB0JhbSMKoLJO4DJJGZdCnLdG4yVKgvYDVTOBLL7XYofuwaca3tXxqEGccRPAwxvpJxnQ==";
            const string legacyClientSecret = "BhOpd6467I3WHEXmtGNrD7IX1svvn4pqK7D5E0d8lnFUvxH8acFqBRs2+EHcKxVKECAVjhzTRzWSQ1Zd+LYRGQ==";

            var graphSettings = context.GraphSettings.ToList();
            var anyChanges = false;

            foreach (var graphSetting in graphSettings)
            {
                var matchesLegacySeed = string.Equals(graphSetting.ClientId, legacyClientId, StringComparison.Ordinal) &&
                                        string.Equals(graphSetting.TenantId, legacyTenantId, StringComparison.Ordinal) &&
                                        string.Equals(graphSetting.ClientSecret, legacyClientSecret, StringComparison.Ordinal);

                if (!matchesLegacySeed)
                {
                    continue;
                }

                graphSetting.ClientId = string.Empty;
                graphSetting.TenantId = string.Empty;
                graphSetting.ClientSecret = string.Empty;
                anyChanges = true;
            }

            if (anyChanges)
            {
                context.SaveChanges();
            }
        }

        private string[] ReadScopesFromConfiguration()
        {
            return _configuration
                .GetSection("Graph:Scopes")
                .GetChildren()
                .Select(section => section.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void EnsureCompanySchema(ConfigurationDbContext context)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State == ConnectionState.Closed;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(Companies);";

                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"] is string name)
                        {
                            columns.Add(name);
                        }
                    }
                }

                void AddColumnIfMissing(string columnName, string columnDefinition)
                {
                    if (columns.Contains(columnName))
                    {
                        return;
                    }

                    using var addCommand = connection.CreateCommand();
                    addCommand.CommandText = $"ALTER TABLE Companies ADD COLUMN {columnDefinition};";
                    addCommand.ExecuteNonQuery();
                    columns.Add(columnName);
                }

                AddColumnIfMissing("RecordType", $"RecordType TEXT NOT NULL DEFAULT '{Company.RecordTypeSetting}'");
                AddColumnIfMissing("ProcessedSubject", "ProcessedSubject TEXT");
                AddColumnIfMissing("ProcessedDate", "ProcessedDate TEXT");
                AddColumnIfMissing("ProcessedAttachmentsCount", "ProcessedAttachmentsCount INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing("ReportOutputFolder", "ReportOutputFolder TEXT");
                AddColumnIfMissing("AttachmentKeywords", "AttachmentKeywords TEXT NOT NULL DEFAULT '[]'");
                AddColumnIfMissing("ProcessingTag", $"ProcessingTag TEXT NOT NULL DEFAULT '{Company.DefaultProcessingTag}'");

                using var normalizeCommand = connection.CreateCommand();
                normalizeCommand.CommandText = $@"
UPDATE Companies
SET
    Name = COALESCE(Name, ''),
    Mail = COALESCE(Mail, ''),
    StartFrom = COALESCE(StartFrom, ''),
    MailBox = COALESCE(MailBox, '[]'),
    FileTypes = COALESCE(FileTypes, '[]'),
    AttachmentKeywords = COALESCE(AttachmentKeywords, '[]'),
    StorageFolder = COALESCE(StorageFolder, ''),
    ReportOutputFolder = COALESCE(ReportOutputFolder, ''),
    ProcessingTag = COALESCE(ProcessingTag, '{Company.DefaultProcessingTag}'),
    RecordType = COALESCE(RecordType, '{Company.RecordTypeSetting}'),
    ProcessedSubject = COALESCE(ProcessedSubject, ''),
    ProcessedAttachmentsCount = COALESCE(ProcessedAttachmentsCount, 0);";
                normalizeCommand.ExecuteNonQuery();
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        public async Task<Setting> GetSettingsAsync()
        {
            using var context = CreateContext();

            var companies = await context.Companies
                .Where(company => company.RecordType == Company.RecordTypeSetting)
                .ToListAsync();

            var triggers = await context.Triggers.ToListAsync();
            var appSettings = await context.AppSettings.ToListAsync();

            return new Setting
            {
                BaseStorageFolder = appSettings.FirstOrDefault(setting => setting.Key == Setting.KeyBaseStorageFolder)?.Value ?? string.Empty,
                MailSubjectKeywords = appSettings.FirstOrDefault(setting => setting.Key == Setting.KeyMailSubjectKeywords)?.Value ?? string.Empty,
                ProcessingTag = appSettings.FirstOrDefault(setting => setting.Key == Setting.KeyProcessingTag)?.Value ?? Company.DefaultProcessingTag,
                CompanySettings = companies,
                TriggerSettings = triggers
            };
        }

        public async Task UpdateSettingsAsync(Setting setting)
        {
            using var context = CreateContext();

            var baseFolder = await context.AppSettings.FindAsync(Setting.KeyBaseStorageFolder);
            if (baseFolder is null)
            {
                context.AppSettings.Add(new AppSetting { Key = Setting.KeyBaseStorageFolder, Value = setting.BaseStorageFolder });
            }
            else
            {
                baseFolder.Value = setting.BaseStorageFolder;
            }

            var keywords = await context.AppSettings.FindAsync(Setting.KeyMailSubjectKeywords);
            if (keywords is null)
            {
                context.AppSettings.Add(new AppSetting { Key = Setting.KeyMailSubjectKeywords, Value = setting.MailSubjectKeywords });
            }
            else
            {
                keywords.Value = setting.MailSubjectKeywords;
            }

            var processingTag = await context.AppSettings.FindAsync(Setting.KeyProcessingTag);
            if (processingTag is null)
            {
                context.AppSettings.Add(new AppSetting { Key = Setting.KeyProcessingTag, Value = setting.ProcessingTag });
            }
            else
            {
                processingTag.Value = setting.ProcessingTag;
            }

            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");
        }

        public async Task<List<Company>> GetCompaniesAsync()
        {
            using var context = CreateContext();

            return await context.Companies
                .Where(company => company.RecordType == Company.RecordTypeSetting)
                .ToListAsync();
        }

        public async Task<Company?> GetCompanyByIdAsync(Guid id)
        {
            using var context = CreateContext();

            return await context.Companies
                .Where(company => company.RecordType == Company.RecordTypeSetting && company.Id == id)
                .SingleOrDefaultAsync();
        }

        public async Task<Result> AddOrUpdateCompanyAsync(Company company)
        {
            using var context = CreateContext();

            var existing = await context.Companies
                .Where(storedCompany => storedCompany.RecordType == Company.RecordTypeSetting && storedCompany.Id == company.Id)
                .SingleOrDefaultAsync();

            if (existing is not null)
            {
                existing.Name = company.Name;
                existing.Mail = company.Mail;
                existing.StartFrom = company.StartFrom;
                existing.MailBox = company.MailBox?.ToList() ?? [];
                existing.FileTypes = company.FileTypes?.ToList() ?? [];
                existing.AttachmentKeywords = company.AttachmentKeywords?.ToList() ?? [];
                existing.StorageFolder = company.StorageFolder;
                existing.ReportOutputFolder = company.ReportOutputFolder;
                existing.ProcessingTag = string.IsNullOrWhiteSpace(company.ProcessingTag)
                    ? Company.DefaultProcessingTag
                    : company.ProcessingTag;
                existing.RecordType = Company.RecordTypeSetting;
                existing.ProcessedSubject = string.Empty;
                existing.ProcessedDate = null;
                existing.ProcessedAttachmentsCount = 0;
            }
            else
            {
                context.Companies.Add(new Company
                {
                    Id = company.Id,
                    Name = company.Name,
                    Mail = company.Mail,
                    StartFrom = company.StartFrom,
                    MailBox = company.MailBox?.ToList() ?? [],
                    FileTypes = company.FileTypes?.ToList() ?? [],
                    AttachmentKeywords = company.AttachmentKeywords?.ToList() ?? [],
                    StorageFolder = company.StorageFolder,
                    ReportOutputFolder = company.ReportOutputFolder,
                    ProcessingTag = string.IsNullOrWhiteSpace(company.ProcessingTag)
                        ? Company.DefaultProcessingTag
                        : company.ProcessingTag,
                    RecordType = Company.RecordTypeSetting
                });
            }

            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");

            return Result.Success();
        }

        public async Task<Result> DeleteCompanyAsync(Guid id)
        {
            using var context = CreateContext();

            var company = await context.Companies
                .Where(storedCompany => storedCompany.RecordType == Company.RecordTypeSetting && storedCompany.Id == id)
                .SingleOrDefaultAsync();

            if (company is null)
            {
                return Result.Failure(Error.Failure);
            }

            context.Companies.Remove(company);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");

            return Result.Success();
        }

        public async Task<List<Trigger>> GetTriggersAsync()
        {
            using var context = CreateContext();
            return await context.Triggers.ToListAsync();
        }

        public async Task<Trigger?> GetTriggerByIdAsync(Guid id)
        {
            using var context = CreateContext();
            return await context.Triggers.FindAsync(id);
        }

        public async Task<Result> AddOrUpdateTriggerAsync(Trigger trigger)
        {
            using var context = CreateContext();

            var existing = await context.Triggers.FindAsync(trigger.Id);
            if (existing is not null)
            {
                existing.Name = trigger.Name;
                existing.CronExpression = trigger.CronExpression;
            }
            else
            {
                context.Triggers.Add(trigger);
            }

            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");

            return Result.Success();
        }

        public async Task<Result> DeleteTriggerAsync(Guid id)
        {
            using var context = CreateContext();

            var trigger = await context.Triggers.FindAsync(id);
            if (trigger is null)
            {
                return Result.Failure(Error.Failure);
            }

            context.Triggers.Remove(trigger);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");

            return Result.Success();
        }

        public async Task<GraphSetting?> GetGraphSettingsAsync()
        {
            using var context = CreateContext();
            return await context.GraphSettings.FirstOrDefaultAsync();
        }

        public async Task<Result> UpdateGraphSettingsAsync(GraphSetting settings)
        {
            using var context = CreateContext();

            var existing = await context.GraphSettings.FirstOrDefaultAsync();
            if (existing is not null)
            {
                existing.Instance = settings.Instance;
                existing.ClientId = settings.ClientId;
                existing.TenantId = settings.TenantId;
                existing.ClientSecret = settings.ClientSecret;
                existing.GraphUserScopesJson = settings.GraphUserScopesJson;
            }
            else
            {
                context.GraphSettings.Add(settings);
            }

            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");

            return Result.Success();
        }
    }
}


