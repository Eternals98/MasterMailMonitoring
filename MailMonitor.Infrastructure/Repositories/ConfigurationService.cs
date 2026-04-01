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
            EnsureSystemSettingsSchema(context);
            EnsureGlobalSearchKeywordsSchema(context);
            EnsureGraphSettingsSchema(context);
            context.EnsureEmailStatisticsSchema();

            SeedDefaultSettings(context);
            SeedDefaultSystemSettings(context);
            SeedDefaultGlobalSearchKeywords(context);
            SeedDefaultCompany(context);
            SeedDefaultTrigger(context);
            SeedDefaultGraphSettings(context);
            RemoveLegacySeededGraphSecrets(context);
            BackfillGraphSettingsFromLegacyAppSettings(context);
            EnsureSingleGraphSettingsRecord(context);
        }

        private ConfigurationDbContext CreateContext()
        {
            return new ConfigurationDbContext(_dbPath);
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

        private void SeedDefaultSystemSettings(ConfigurationDbContext context)
        {
            var existing = context.SystemSettings
                .SingleOrDefault(setting => setting.Id == SystemSetting.SingletonId);

            if (existing is not null)
            {
                ApplyLegacyBackfill(context, existing);
                return;
            }

            var appSettings = context.AppSettings.ToDictionary(
                setting => setting.Key,
                setting => setting.Value,
                StringComparer.OrdinalIgnoreCase);

            var baseStorage = appSettings.TryGetValue(Setting.KeyBaseStorageFolder, out var baseStorageValue) &&
                              !string.IsNullOrWhiteSpace(baseStorageValue)
                ? baseStorageValue.Trim()
                : @"C:\\OnBase";

            var mailKeywords = appSettings.TryGetValue(Setting.KeyMailSubjectKeywords, out var mailKeywordsValue)
                ? SplitValues(mailKeywordsValue)
                : new[] { "TestOnBase" };

            var processingTag = appSettings.TryGetValue(Setting.KeyProcessingTag, out var processingTagValue) &&
                                !string.IsNullOrWhiteSpace(processingTagValue)
                ? processingTagValue.Trim()
                : Company.DefaultProcessingTag;

            var seeded = new SystemSetting
            {
                Id = SystemSetting.SingletonId,
                BaseStorageFolder = baseStorage,
                DefaultReportOutputFolder = @"\\Reports",
                DefaultProcessingTag = processingTag,
                MailSubjectKeywordsJson = ConfigurationDbContext.SerializeStringList(mailKeywords),
                DefaultFileTypesJson = ConfigurationDbContext.SerializeStringList([]),
                DefaultAttachmentKeywordsJson = ConfigurationDbContext.SerializeStringList([]),
                SchedulerTimeZoneId = _configuration["Scheduling:TimeZoneId"] ?? Setting.DefaultSchedulerTimeZoneId,
                SchedulerFallbackCronExpression = _configuration["Scheduling:FallbackCronExpression"] ?? Setting.DefaultSchedulerFallbackCronExpression,
                StorageMaxRetries = ReadIntFromConfiguration("Storage:MaxRetries", 3),
                StorageBaseDelayMs = ReadIntFromConfiguration("Storage:BaseDelayMilliseconds", 300),
                StorageMaxDelayMs = ReadIntFromConfiguration("Storage:MaxDelayMilliseconds", 4000),
                GraphHealthCheckEnabled = true,
                MailboxSearchEnabled = true,
                ProcessingActionsEnabled = true,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedBy = "seed",
                Revision = 1
            };

            if (seeded.StorageMaxDelayMs < seeded.StorageBaseDelayMs)
            {
                seeded.StorageMaxDelayMs = seeded.StorageBaseDelayMs;
            }

            context.SystemSettings.Add(seeded);
            context.SaveChanges();
        }

        private static void ApplyLegacyBackfill(ConfigurationDbContext context, SystemSetting systemSetting)
        {
            var appSettings = context.AppSettings.ToDictionary(
                setting => setting.Key,
                setting => setting.Value,
                StringComparer.OrdinalIgnoreCase);

            var changed = false;

            if (string.IsNullOrWhiteSpace(systemSetting.BaseStorageFolder) &&
                appSettings.TryGetValue(Setting.KeyBaseStorageFolder, out var baseStorage) &&
                !string.IsNullOrWhiteSpace(baseStorage))
            {
                systemSetting.BaseStorageFolder = baseStorage.Trim();
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(systemSetting.DefaultProcessingTag) &&
                appSettings.TryGetValue(Setting.KeyProcessingTag, out var processingTag) &&
                !string.IsNullOrWhiteSpace(processingTag))
            {
                systemSetting.DefaultProcessingTag = processingTag.Trim();
                changed = true;
            }

            var hasEmptyKeywords = string.IsNullOrWhiteSpace(systemSetting.MailSubjectKeywordsJson) ||
                                   string.Equals(systemSetting.MailSubjectKeywordsJson.Trim(), "[]", StringComparison.Ordinal);

            if (hasEmptyKeywords &&
                appSettings.TryGetValue(Setting.KeyMailSubjectKeywords, out var keywords) &&
                !string.IsNullOrWhiteSpace(keywords))
            {
                systemSetting.MailSubjectKeywordsJson = ConfigurationDbContext.SerializeStringList(SplitValues(keywords));
                changed = true;
            }

            if (changed)
            {
                systemSetting.UpdatedAtUtc = DateTime.UtcNow;
                systemSetting.UpdatedBy = "legacy-backfill";
                systemSetting.Revision = Math.Max(1, systemSetting.Revision + 1);
                context.SaveChanges();
            }
        }

        private static void SeedDefaultGlobalSearchKeywords(ConfigurationDbContext context)
        {
            if (context.GlobalSearchKeywords.Any())
            {
                return;
            }

            var keywords = context.SystemSettings
                .Where(setting => setting.Id == SystemSetting.SingletonId)
                .Select(setting => setting.MailSubjectKeywordsJson)
                .AsEnumerable()
                .SelectMany(serialized => ConfigurationDbContext.DeserializeStringList(serialized))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (keywords.Length == 0)
            {
                keywords = ["TestOnBase"];
            }

            ReplaceGlobalSearchKeywords(context, keywords);
            context.SaveChanges();
        }

        private static IReadOnlyCollection<string> GetGlobalSearchKeywords(ConfigurationDbContext context)
        {
            return context.GlobalSearchKeywords
                .AsNoTracking()
                .Where(item => item.IsEnabled)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Keyword)
                .Select(item => item.Keyword)
                .ToArray();
        }

        private static void ReplaceGlobalSearchKeywords(ConfigurationDbContext context, IEnumerable<string>? keywords)
        {
            var normalizedKeywords = keywords?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            var existing = context.GlobalSearchKeywords.ToList();
            var existingByKeyword = existing.ToDictionary(item => item.Keyword, StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var position = 0;

            foreach (var keyword in normalizedKeywords)
            {
                if (existingByKeyword.TryGetValue(keyword, out var stored))
                {
                    stored.IsEnabled = true;
                    stored.SortOrder = position++;
                    stored.UpdatedAtUtc = now;
                    continue;
                }

                context.GlobalSearchKeywords.Add(new GlobalSearchKeyword
                {
                    Id = Guid.NewGuid(),
                    Keyword = keyword,
                    IsEnabled = true,
                    SortOrder = position++,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            var newSet = normalizedKeywords.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in existing)
            {
                if (newSet.Contains(item.Keyword))
                {
                    continue;
                }

                context.GlobalSearchKeywords.Remove(item);
            }

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
                OverrideGlobalProcessingTag = true,
                OverrideGlobalStorageFolder = false,
                OverrideGlobalReportOutputFolder = false,
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
            if (context.GraphSettings.Any(item => item.Id == GraphSetting.SingletonId))
            {
                return;
            }

            var configuredScopes = ReadScopesFromConfiguration();
            var scopes = configuredScopes is { Length: > 0 }
                ? configuredScopes
                : new[] { "https://graph.microsoft.com/.default" };

            var defaultGraphSettings = new GraphSetting
            {
                Id = GraphSetting.SingletonId,
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

        private static IReadOnlyCollection<string> SplitValues(string? values)
        {
            if (string.IsNullOrWhiteSpace(values))
            {
                return [];
            }

            return values
                .Split([',', ';', '|', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private int ReadIntFromConfiguration(string key, int fallback)
        {
            return int.TryParse(_configuration[key], out var value)
                ? value
                : fallback;
        }

        private static void EnsureSystemSettingsSchema(ConfigurationDbContext context)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State == ConnectionState.Closed;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = $@"
CREATE TABLE IF NOT EXISTS SystemSettings (
    Id INTEGER NOT NULL PRIMARY KEY,
    BaseStorageFolder TEXT NOT NULL DEFAULT '',
    DefaultReportOutputFolder TEXT NOT NULL DEFAULT '',
    DefaultProcessingTag TEXT NOT NULL DEFAULT '{Company.DefaultProcessingTag}',
    MailSubjectKeywordsJson TEXT NOT NULL DEFAULT '[]',
    DefaultFileTypesJson TEXT NOT NULL DEFAULT '[]',
    DefaultAttachmentKeywordsJson TEXT NOT NULL DEFAULT '[]',
    SchedulerTimeZoneId TEXT NOT NULL DEFAULT '{Setting.DefaultSchedulerTimeZoneId}',
    SchedulerFallbackCronExpression TEXT NOT NULL DEFAULT '{Setting.DefaultSchedulerFallbackCronExpression}',
    StorageMaxRetries INTEGER NOT NULL DEFAULT 3,
    StorageBaseDelayMs INTEGER NOT NULL DEFAULT 300,
    StorageMaxDelayMs INTEGER NOT NULL DEFAULT 4000,
    GraphHealthCheckEnabled INTEGER NOT NULL DEFAULT 1,
    MailboxSearchEnabled INTEGER NOT NULL DEFAULT 1,
    ProcessingActionsEnabled INTEGER NOT NULL DEFAULT 1,
    UpdatedAtUtc TEXT NOT NULL DEFAULT (datetime('now')),
    UpdatedBy TEXT NULL,
    Revision INTEGER NOT NULL DEFAULT 1
);";
                createTableCommand.ExecuteNonQuery();

                using var columnsCommand = connection.CreateCommand();
                columnsCommand.CommandText = "PRAGMA table_info(SystemSettings);";

                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = columnsCommand.ExecuteReader())
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
                    addCommand.CommandText = $"ALTER TABLE SystemSettings ADD COLUMN {columnDefinition};";
                    addCommand.ExecuteNonQuery();
                    columns.Add(columnName);
                }

                AddColumnIfMissing("DefaultReportOutputFolder", "DefaultReportOutputFolder TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing("DefaultProcessingTag", $"DefaultProcessingTag TEXT NOT NULL DEFAULT '{Company.DefaultProcessingTag}'");
                AddColumnIfMissing("MailSubjectKeywordsJson", "MailSubjectKeywordsJson TEXT NOT NULL DEFAULT '[]'");
                AddColumnIfMissing("DefaultFileTypesJson", "DefaultFileTypesJson TEXT NOT NULL DEFAULT '[]'");
                AddColumnIfMissing("DefaultAttachmentKeywordsJson", "DefaultAttachmentKeywordsJson TEXT NOT NULL DEFAULT '[]'");
                AddColumnIfMissing("SchedulerTimeZoneId", $"SchedulerTimeZoneId TEXT NOT NULL DEFAULT '{Setting.DefaultSchedulerTimeZoneId}'");
                AddColumnIfMissing("SchedulerFallbackCronExpression", $"SchedulerFallbackCronExpression TEXT NOT NULL DEFAULT '{Setting.DefaultSchedulerFallbackCronExpression}'");
                AddColumnIfMissing("StorageMaxRetries", "StorageMaxRetries INTEGER NOT NULL DEFAULT 3");
                AddColumnIfMissing("StorageBaseDelayMs", "StorageBaseDelayMs INTEGER NOT NULL DEFAULT 300");
                AddColumnIfMissing("StorageMaxDelayMs", "StorageMaxDelayMs INTEGER NOT NULL DEFAULT 4000");
                AddColumnIfMissing("GraphHealthCheckEnabled", "GraphHealthCheckEnabled INTEGER NOT NULL DEFAULT 1");
                AddColumnIfMissing("MailboxSearchEnabled", "MailboxSearchEnabled INTEGER NOT NULL DEFAULT 1");
                AddColumnIfMissing("ProcessingActionsEnabled", "ProcessingActionsEnabled INTEGER NOT NULL DEFAULT 1");
                AddColumnIfMissing("UpdatedAtUtc", "UpdatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))");
                AddColumnIfMissing("UpdatedBy", "UpdatedBy TEXT NULL");
                AddColumnIfMissing("Revision", "Revision INTEGER NOT NULL DEFAULT 1");

                using var normalizeCommand = connection.CreateCommand();
                normalizeCommand.CommandText = $@"
UPDATE SystemSettings
SET
    BaseStorageFolder = COALESCE(BaseStorageFolder, ''),
    DefaultReportOutputFolder = COALESCE(DefaultReportOutputFolder, ''),
    DefaultProcessingTag = COALESCE(DefaultProcessingTag, '{Company.DefaultProcessingTag}'),
    MailSubjectKeywordsJson = COALESCE(MailSubjectKeywordsJson, '[]'),
    DefaultFileTypesJson = COALESCE(DefaultFileTypesJson, '[]'),
    DefaultAttachmentKeywordsJson = COALESCE(DefaultAttachmentKeywordsJson, '[]'),
    SchedulerTimeZoneId = COALESCE(SchedulerTimeZoneId, '{Setting.DefaultSchedulerTimeZoneId}'),
    SchedulerFallbackCronExpression = COALESCE(SchedulerFallbackCronExpression, '{Setting.DefaultSchedulerFallbackCronExpression}'),
    StorageMaxRetries = COALESCE(StorageMaxRetries, 3),
    StorageBaseDelayMs = COALESCE(StorageBaseDelayMs, 300),
    StorageMaxDelayMs = COALESCE(StorageMaxDelayMs, 4000),
    GraphHealthCheckEnabled = COALESCE(GraphHealthCheckEnabled, 1),
    MailboxSearchEnabled = COALESCE(MailboxSearchEnabled, 1),
    ProcessingActionsEnabled = COALESCE(ProcessingActionsEnabled, 1),
    UpdatedAtUtc = COALESCE(UpdatedAtUtc, datetime('now')),
    Revision = COALESCE(Revision, 1);";
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

        private static void BackfillGraphSettingsFromLegacyAppSettings(ConfigurationDbContext context)
        {
            var graphSettings = context.GraphSettings
                .OrderByDescending(item => item.Id)
                .ToList();

            var target = SelectPreferredGraphSettings(graphSettings);
            if (target is null)
            {
                return;
            }

            var appSettings = context.AppSettings
                .AsNoTracking()
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

            string ReadValue(params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (appSettings.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
                    {
                        return rawValue.Trim();
                    }
                }

                return string.Empty;
            }

            var changed = false;

            if (string.IsNullOrWhiteSpace(target.Instance))
            {
                var value = ReadValue("Graph:Instance", "GraphInstance", "GraphAuthority", "GraphUrl");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Instance = value;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(target.ClientId))
            {
                var value = ReadValue("Graph:ClientId", "GraphClientId", "ClientId");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.ClientId = value;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(target.TenantId))
            {
                var value = ReadValue("Graph:TenantId", "GraphTenantId", "TenantId");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.TenantId = value;
                    changed = true;
                }
            }

            if (string.IsNullOrWhiteSpace(target.ClientSecret))
            {
                var value = ReadValue("Graph:ClientSecret", "GraphClientSecret", "ClientSecret");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.ClientSecret = value;
                    changed = true;
                }
            }

            var hasScopes = !string.IsNullOrWhiteSpace(target.GraphUserScopesJson) &&
                            !string.Equals(target.GraphUserScopesJson.Trim(), "[]", StringComparison.Ordinal);

            if (!hasScopes)
            {
                var rawScopes = ReadValue("GraphUserScopesJson", "Graph:ScopesJson", "GraphScopesJson", "GraphScopes");
                if (!string.IsNullOrWhiteSpace(rawScopes))
                {
                    if (rawScopes.TrimStart().StartsWith("[", StringComparison.Ordinal))
                    {
                        target.GraphUserScopesJson = rawScopes;
                    }
                    else
                    {
                        target.GraphUserScopesJson = JsonSerializer.Serialize(SplitValues(rawScopes));
                    }

                    changed = true;
                }
            }

            if (changed)
            {
                context.SaveChanges();
            }
        }

        private static void EnsureGraphSettingsSchema(ConfigurationDbContext context)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State == ConnectionState.Closed;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
CREATE TABLE IF NOT EXISTS GraphSettings (
    Id INTEGER NOT NULL PRIMARY KEY,
    Instance TEXT NOT NULL DEFAULT '',
    ClientId TEXT NOT NULL DEFAULT '',
    TenantId TEXT NOT NULL DEFAULT '',
    ClientSecret TEXT NOT NULL DEFAULT '',
    GraphUserScopesJson TEXT NOT NULL DEFAULT '[]',
    LastVerificationAtUtc TEXT NULL,
    LastVerificationSucceeded INTEGER NULL,
    LastVerificationErrorCode TEXT NOT NULL DEFAULT '',
    LastVerificationErrorMessage TEXT NOT NULL DEFAULT ''
);";
                createTableCommand.ExecuteNonQuery();

                using var columnsCommand = connection.CreateCommand();
                columnsCommand.CommandText = "PRAGMA table_info(GraphSettings);";

                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = columnsCommand.ExecuteReader())
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
                    addCommand.CommandText = $"ALTER TABLE GraphSettings ADD COLUMN {columnDefinition};";
                    addCommand.ExecuteNonQuery();
                    columns.Add(columnName);
                }

                AddColumnIfMissing("GraphUserScopesJson", "GraphUserScopesJson TEXT NOT NULL DEFAULT '[]'");
                AddColumnIfMissing("LastVerificationAtUtc", "LastVerificationAtUtc TEXT NULL");
                AddColumnIfMissing("LastVerificationSucceeded", "LastVerificationSucceeded INTEGER NULL");
                AddColumnIfMissing("LastVerificationErrorCode", "LastVerificationErrorCode TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing("LastVerificationErrorMessage", "LastVerificationErrorMessage TEXT NOT NULL DEFAULT ''");

                using var normalizeCommand = connection.CreateCommand();
                normalizeCommand.CommandText = @"
UPDATE GraphSettings
SET
    Instance = COALESCE(Instance, ''),
    ClientId = COALESCE(ClientId, ''),
    TenantId = COALESCE(TenantId, ''),
    ClientSecret = COALESCE(ClientSecret, ''),
    GraphUserScopesJson = COALESCE(GraphUserScopesJson, '[]'),
    LastVerificationErrorCode = COALESCE(LastVerificationErrorCode, ''),
    LastVerificationErrorMessage = COALESCE(LastVerificationErrorMessage, '');";
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

        private static void EnsureSingleGraphSettingsRecord(ConfigurationDbContext context)
        {
            var all = context.GraphSettings
                .OrderByDescending(item => item.Id)
                .ToList();

            if (all.Count == 0)
            {
                return;
            }

            var preferred = SelectPreferredGraphSettings(all) ?? all[0];
            var singleton = all.SingleOrDefault(item => item.Id == GraphSetting.SingletonId);
            var hasChanges = false;

            if (singleton is null)
            {
                singleton = new GraphSetting { Id = GraphSetting.SingletonId };
                context.GraphSettings.Add(singleton);
                hasChanges = true;
            }

            if (!AreEquivalentGraphSettings(singleton, preferred))
            {
                CopyGraphSettings(singleton, preferred);
                hasChanges = true;
            }

            foreach (var item in all.Where(item => item.Id != GraphSetting.SingletonId))
            {
                context.GraphSettings.Remove(item);
                hasChanges = true;
            }

            if (hasChanges)
            {
                context.SaveChanges();
            }
        }

        private static void CopyGraphSettings(GraphSetting target, GraphSetting source)
        {
            target.Instance = source.Instance;
            target.ClientId = source.ClientId;
            target.TenantId = source.TenantId;
            target.ClientSecret = source.ClientSecret;
            target.GraphUserScopesJson = source.GraphUserScopesJson;
            target.LastVerificationAtUtc = source.LastVerificationAtUtc;
            target.LastVerificationSucceeded = source.LastVerificationSucceeded;
            target.LastVerificationErrorCode = source.LastVerificationErrorCode;
            target.LastVerificationErrorMessage = source.LastVerificationErrorMessage;
        }

        private static bool AreEquivalentGraphSettings(GraphSetting left, GraphSetting right)
        {
            return string.Equals(left.Instance, right.Instance, StringComparison.Ordinal) &&
                   string.Equals(left.ClientId, right.ClientId, StringComparison.Ordinal) &&
                   string.Equals(left.TenantId, right.TenantId, StringComparison.Ordinal) &&
                   string.Equals(left.ClientSecret, right.ClientSecret, StringComparison.Ordinal) &&
                   string.Equals(left.GraphUserScopesJson, right.GraphUserScopesJson, StringComparison.Ordinal) &&
                   left.LastVerificationAtUtc == right.LastVerificationAtUtc &&
                   left.LastVerificationSucceeded == right.LastVerificationSucceeded &&
                   string.Equals(left.LastVerificationErrorCode, right.LastVerificationErrorCode, StringComparison.Ordinal) &&
                   string.Equals(left.LastVerificationErrorMessage, right.LastVerificationErrorMessage, StringComparison.Ordinal);
        }

        private static void EnsureGlobalSearchKeywordsSchema(ConfigurationDbContext context)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State == ConnectionState.Closed;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
CREATE TABLE IF NOT EXISTS GlobalSearchKeywords (
    Id TEXT NOT NULL PRIMARY KEY,
    Keyword TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now')),
    UpdatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))
);";
                createTableCommand.ExecuteNonQuery();

                using var columnsCommand = connection.CreateCommand();
                columnsCommand.CommandText = "PRAGMA table_info(GlobalSearchKeywords);";

                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = columnsCommand.ExecuteReader())
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
                    addCommand.CommandText = $"ALTER TABLE GlobalSearchKeywords ADD COLUMN {columnDefinition};";
                    addCommand.ExecuteNonQuery();
                    columns.Add(columnName);
                }

                AddColumnIfMissing("IsEnabled", "IsEnabled INTEGER NOT NULL DEFAULT 1");
                AddColumnIfMissing("SortOrder", "SortOrder INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing("CreatedAtUtc", "CreatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))");
                AddColumnIfMissing("UpdatedAtUtc", "UpdatedAtUtc TEXT NOT NULL DEFAULT (datetime('now'))");

                using var normalizeCommand = connection.CreateCommand();
                normalizeCommand.CommandText = @"
UPDATE GlobalSearchKeywords
SET
    Id = COALESCE(NULLIF(Id, ''), lower(hex(randomblob(16)))),
    Keyword = COALESCE(Keyword, ''),
    IsEnabled = COALESCE(IsEnabled, 1),
    SortOrder = COALESCE(SortOrder, 0),
    CreatedAtUtc = COALESCE(CreatedAtUtc, datetime('now')),
    UpdatedAtUtc = COALESCE(UpdatedAtUtc, datetime('now'));";
                normalizeCommand.ExecuteNonQuery();

                using var dedupeCommand = connection.CreateCommand();
                dedupeCommand.CommandText = @"
DELETE FROM GlobalSearchKeywords
WHERE rowid NOT IN (
    SELECT MIN(rowid)
    FROM GlobalSearchKeywords
    GROUP BY lower(trim(Keyword))
);";
                dedupeCommand.ExecuteNonQuery();

                using var uniqueIndexCommand = connection.CreateCommand();
                uniqueIndexCommand.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_GlobalSearchKeywords_Keyword ON GlobalSearchKeywords(Keyword COLLATE NOCASE);";
                uniqueIndexCommand.ExecuteNonQuery();

                using var sortIndexCommand = connection.CreateCommand();
                sortIndexCommand.CommandText = "CREATE INDEX IF NOT EXISTS IX_GlobalSearchKeywords_SortOrder ON GlobalSearchKeywords(SortOrder);";
                sortIndexCommand.ExecuteNonQuery();
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
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
                AddColumnIfMissing("OverrideGlobalProcessingTag", "OverrideGlobalProcessingTag INTEGER NOT NULL DEFAULT 1");
                AddColumnIfMissing("OverrideGlobalStorageFolder", "OverrideGlobalStorageFolder INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing("OverrideGlobalReportOutputFolder", "OverrideGlobalReportOutputFolder INTEGER NOT NULL DEFAULT 0");

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
    OverrideGlobalProcessingTag = COALESCE(OverrideGlobalProcessingTag, 1),
    OverrideGlobalStorageFolder = COALESCE(OverrideGlobalStorageFolder, 0),
    OverrideGlobalReportOutputFolder = COALESCE(OverrideGlobalReportOutputFolder, 0),
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
            var systemSettings = await context.SystemSettings
                .SingleOrDefaultAsync(setting => setting.Id == SystemSetting.SingletonId);

            if (systemSettings is null)
            {
                SeedDefaultSystemSettings(context);
                systemSettings = await context.SystemSettings
                    .SingleAsync(setting => setting.Id == SystemSetting.SingletonId);
            }

            var globalSearchKeywords = GetGlobalSearchKeywords(context);

            return new Setting
            {
                BaseStorageFolder = systemSettings.BaseStorageFolder,
                MailSubjectKeywords = string.Join(",", globalSearchKeywords),
                ProcessingTag = systemSettings.DefaultProcessingTag,
                DefaultReportOutputFolder = systemSettings.DefaultReportOutputFolder,
                DefaultFileTypes = string.Join(",", ConfigurationDbContext.DeserializeStringList(systemSettings.DefaultFileTypesJson)),
                DefaultAttachmentKeywords = string.Join(",", ConfigurationDbContext.DeserializeStringList(systemSettings.DefaultAttachmentKeywordsJson)),
                SchedulerTimeZoneId = systemSettings.SchedulerTimeZoneId,
                SchedulerFallbackCronExpression = systemSettings.SchedulerFallbackCronExpression,
                StorageMaxRetries = systemSettings.StorageMaxRetries,
                StorageBaseDelayMs = systemSettings.StorageBaseDelayMs,
                StorageMaxDelayMs = systemSettings.StorageMaxDelayMs,
                GraphHealthCheckEnabled = systemSettings.GraphHealthCheckEnabled,
                MailboxSearchEnabled = systemSettings.MailboxSearchEnabled,
                ProcessingActionsEnabled = systemSettings.ProcessingActionsEnabled,
                UpdatedAtUtc = systemSettings.UpdatedAtUtc,
                UpdatedBy = systemSettings.UpdatedBy ?? string.Empty,
                Revision = systemSettings.Revision,
                CompanySettings = companies,
                TriggerSettings = triggers
            };
        }

        public async Task UpdateSettingsAsync(Setting setting)
        {
            using var context = CreateContext();
            var normalizedKeywords = SplitValues(setting.MailSubjectKeywords);

            var systemSetting = await context.SystemSettings
                .SingleOrDefaultAsync(entity => entity.Id == SystemSetting.SingletonId);

            if (systemSetting is null)
            {
                systemSetting = new SystemSetting { Id = SystemSetting.SingletonId };
                context.SystemSettings.Add(systemSetting);
            }

            systemSetting.BaseStorageFolder = setting.BaseStorageFolder.Trim();
            systemSetting.DefaultReportOutputFolder = setting.DefaultReportOutputFolder.Trim();
            systemSetting.DefaultProcessingTag = setting.ProcessingTag.Trim();
            systemSetting.MailSubjectKeywordsJson = ConfigurationDbContext.SerializeStringList(normalizedKeywords);
            systemSetting.DefaultFileTypesJson = ConfigurationDbContext.SerializeStringList(SplitValues(setting.DefaultFileTypes));
            systemSetting.DefaultAttachmentKeywordsJson = ConfigurationDbContext.SerializeStringList(SplitValues(setting.DefaultAttachmentKeywords));
            systemSetting.SchedulerTimeZoneId = string.IsNullOrWhiteSpace(setting.SchedulerTimeZoneId)
                ? Setting.DefaultSchedulerTimeZoneId
                : setting.SchedulerTimeZoneId.Trim();
            systemSetting.SchedulerFallbackCronExpression = string.IsNullOrWhiteSpace(setting.SchedulerFallbackCronExpression)
                ? Setting.DefaultSchedulerFallbackCronExpression
                : setting.SchedulerFallbackCronExpression.Trim();
            systemSetting.StorageMaxRetries = setting.StorageMaxRetries;
            systemSetting.StorageBaseDelayMs = setting.StorageBaseDelayMs;
            systemSetting.StorageMaxDelayMs = setting.StorageMaxDelayMs;
            systemSetting.GraphHealthCheckEnabled = setting.GraphHealthCheckEnabled;
            systemSetting.MailboxSearchEnabled = setting.MailboxSearchEnabled;
            systemSetting.ProcessingActionsEnabled = setting.ProcessingActionsEnabled;
            systemSetting.UpdatedAtUtc = DateTime.UtcNow;
            systemSetting.UpdatedBy = string.IsNullOrWhiteSpace(setting.UpdatedBy) ? "api" : setting.UpdatedBy.Trim();
            systemSetting.Revision = Math.Max(1, systemSetting.Revision + 1);

            ReplaceGlobalSearchKeywords(context, normalizedKeywords);

            UpsertLegacyAppSetting(context, Setting.KeyBaseStorageFolder, systemSetting.BaseStorageFolder);
            UpsertLegacyAppSetting(context, Setting.KeyMailSubjectKeywords, string.Join(",", normalizedKeywords));
            UpsertLegacyAppSetting(context, Setting.KeyProcessingTag, systemSetting.DefaultProcessingTag);

            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");
        }

        private static void UpsertLegacyAppSetting(ConfigurationDbContext context, string key, string value)
        {
            var existing = context.AppSettings.SingleOrDefault(setting => setting.Key == key);
            if (existing is null)
            {
                context.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value
                });

                return;
            }

            existing.Value = value;
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
                existing.OverrideGlobalProcessingTag = company.OverrideGlobalProcessingTag;
                existing.OverrideGlobalStorageFolder = company.OverrideGlobalStorageFolder;
                existing.OverrideGlobalReportOutputFolder = company.OverrideGlobalReportOutputFolder;
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
                    OverrideGlobalProcessingTag = company.OverrideGlobalProcessingTag,
                    OverrideGlobalStorageFolder = company.OverrideGlobalStorageFolder,
                    OverrideGlobalReportOutputFolder = company.OverrideGlobalReportOutputFolder,
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

            return await context.GraphSettings
                .SingleOrDefaultAsync(item => item.Id == GraphSetting.SingletonId);
        }

        public async Task<Result> UpdateGraphSettingsAsync(GraphSetting settings)
        {
            using var context = CreateContext();

            EnsureSingleGraphSettingsRecord(context);

            var existing = await context.GraphSettings
                .SingleOrDefaultAsync(item => item.Id == GraphSetting.SingletonId);

            if (existing is null)
            {
                existing = new GraphSetting { Id = GraphSetting.SingletonId };
                context.GraphSettings.Add(existing);
            }

            var connectionSettingsChanged =
                !string.Equals(existing.Instance, settings.Instance, StringComparison.Ordinal) ||
                !string.Equals(existing.ClientId, settings.ClientId, StringComparison.Ordinal) ||
                !string.Equals(existing.TenantId, settings.TenantId, StringComparison.Ordinal) ||
                !string.Equals(existing.ClientSecret, settings.ClientSecret, StringComparison.Ordinal) ||
                !string.Equals(existing.GraphUserScopesJson, settings.GraphUserScopesJson, StringComparison.Ordinal);

            existing.Instance = settings.Instance;
            existing.ClientId = settings.ClientId;
            existing.TenantId = settings.TenantId;
            existing.ClientSecret = settings.ClientSecret;
            existing.GraphUserScopesJson = settings.GraphUserScopesJson;

            var hasVerificationPayload =
                settings.LastVerificationAtUtc.HasValue ||
                settings.LastVerificationSucceeded.HasValue ||
                !string.IsNullOrWhiteSpace(settings.LastVerificationErrorCode) ||
                !string.IsNullOrWhiteSpace(settings.LastVerificationErrorMessage);

            if (hasVerificationPayload)
            {
                existing.LastVerificationAtUtc = settings.LastVerificationAtUtc;
                existing.LastVerificationSucceeded = settings.LastVerificationSucceeded;
                existing.LastVerificationErrorCode = settings.LastVerificationErrorCode;
                existing.LastVerificationErrorMessage = settings.LastVerificationErrorMessage;
            }
            else if (connectionSettingsChanged)
            {
                existing.ClearVerification();
            }

            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");

            return Result.Success();
        }

        private static GraphSetting? SelectPreferredGraphSettings(IReadOnlyList<GraphSetting> settings)
        {
            return settings.FirstOrDefault(HasGraphCredentials) ?? settings.FirstOrDefault();
        }

        private static bool HasGraphCredentials(GraphSetting settings)
        {
            return !string.IsNullOrWhiteSpace(settings.ClientId) ||
                   !string.IsNullOrWhiteSpace(settings.TenantId) ||
                   !string.IsNullOrWhiteSpace(settings.ClientSecret);
        }
    }
}


