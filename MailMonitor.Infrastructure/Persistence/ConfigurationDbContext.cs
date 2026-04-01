using System.Data;
using System.Text.Json;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Graph;
using MailMonitor.Domain.Entities.Jobs;
using MailMonitor.Domain.Entities.Reporting;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace MailMonitor.Infrastructure.Persistence
{
    public sealed class ConfigurationDbContext : DbContext
    {
        private readonly string? _dbPath;

        public ConfigurationDbContext(string dbPath)
        {
            _dbPath = dbPath;
        }

        public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppSetting> AppSettings => Set<AppSetting>();
        public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
        public DbSet<GlobalSearchKeyword> GlobalSearchKeywords => Set<GlobalSearchKeyword>();
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Trigger> Triggers => Set<Trigger>();
        public DbSet<GraphSetting> GraphSettings => Set<GraphSetting>();
        public DbSet<EmailProcessStatistic> EmailStatistics => Set<EmailProcessStatistic>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_dbPath))
            {
                throw new InvalidOperationException("A valid SQLite database path is required.");
            }

            optionsBuilder.UseSqlite($"Data Source={_dbPath};Cache=Shared");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AppSettingConfiguration());
            modelBuilder.ApplyConfiguration(new SystemSettingConfiguration());
            modelBuilder.ApplyConfiguration(new GlobalSearchKeywordConfiguration());
            modelBuilder.ApplyConfiguration(new CompanyConfiguration());
            modelBuilder.ApplyConfiguration(new TriggerConfiguration());
            modelBuilder.ApplyConfiguration(new GraphSettingConfiguration());
            modelBuilder.ApplyConfiguration(new EmailStatisticConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        public void EnsureEmailStatisticsSchema()
        {
            var connection = Database.GetDbConnection();
            var shouldClose = connection.State == ConnectionState.Closed;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
CREATE TABLE IF NOT EXISTS EmailStatistics (
    Id TEXT NOT NULL PRIMARY KEY,
    Date TEXT NOT NULL,
    CompanyName TEXT NOT NULL,
    UserMail TEXT NOT NULL,
    Processed INTEGER NOT NULL,
    Subject TEXT NOT NULL,
    AttachmentsCount INTEGER NOT NULL DEFAULT 0,
    ReasonIgnored TEXT NOT NULL DEFAULT '',
    Mailbox TEXT NOT NULL DEFAULT '',
    StoredAttachments TEXT NOT NULL DEFAULT '[]',
    StorageFolder TEXT NOT NULL DEFAULT '',
    MessageId TEXT NULL
);";
                createTableCommand.ExecuteNonQuery();

                using var columnsCommand = connection.CreateCommand();
                columnsCommand.CommandText = "PRAGMA table_info(EmailStatistics);";

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
                    addCommand.CommandText = $"ALTER TABLE EmailStatistics ADD COLUMN {columnDefinition};";
                    addCommand.ExecuteNonQuery();
                    columns.Add(columnName);
                }

                AddColumnIfMissing("Id", "Id TEXT");
                AddColumnIfMissing("Date", "Date TEXT");
                AddColumnIfMissing("CompanyName", "CompanyName TEXT");
                AddColumnIfMissing("UserMail", "UserMail TEXT");
                AddColumnIfMissing("Processed", "Processed INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing("Subject", "Subject TEXT");
                AddColumnIfMissing("AttachmentsCount", "AttachmentsCount INTEGER NOT NULL DEFAULT 0");
                AddColumnIfMissing("ReasonIgnored", "ReasonIgnored TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing("Mailbox", "Mailbox TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing("StoredAttachments", "StoredAttachments TEXT NOT NULL DEFAULT '[]'");
                AddColumnIfMissing("StorageFolder", "StorageFolder TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing("MessageId", "MessageId TEXT");

                using var normalizeCommand = connection.CreateCommand();
                normalizeCommand.CommandText = @"
UPDATE EmailStatistics
SET
    Id = COALESCE(NULLIF(Id, ''), lower(hex(randomblob(16)))),
    Date = COALESCE(Date, datetime('now')),
    CompanyName = COALESCE(CompanyName, ''),
    UserMail = COALESCE(UserMail, ''),
    Processed = COALESCE(Processed, 0),
    Subject = COALESCE(Subject, ''),
    AttachmentsCount = COALESCE(AttachmentsCount, 0),
    ReasonIgnored = COALESCE(ReasonIgnored, ''),
    Mailbox = COALESCE(Mailbox, ''),
    StoredAttachments = COALESCE(StoredAttachments, '[]'),
    StorageFolder = COALESCE(StorageFolder, '');";
                normalizeCommand.ExecuteNonQuery();

                using var indexCommand = connection.CreateCommand();
                indexCommand.CommandText = "CREATE INDEX IF NOT EXISTS IX_EmailStatistics_MessageId ON EmailStatistics(MessageId);";
                indexCommand.ExecuteNonQuery();
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        internal static IReadOnlyList<string> DeserializeStringList(string? serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return Array.Empty<string>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(serialized) ?? new List<string>();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        internal static string SerializeStringList(IEnumerable<string>? values)
        {
            var normalized = values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            return JsonSerializer.Serialize(normalized);
        }
    }
}

