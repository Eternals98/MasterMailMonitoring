using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Application.Abstractions.Graph;
using MailMonitor.Application.Abstractions.Reporting;
using MailMonitor.Application.Abstractions.Storage;
using MailMonitor.Domain.Repositories;
using MailMonitor.Infrastructure.Configuration;
using MailMonitor.Infrastructure.Graph;
using MailMonitor.Infrastructure.Reporting;
using MailMonitor.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MailMonitor.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var dbPath = ResolveDatabasePath(configuration);

            services.AddSingleton(provider => dbPath);

            services.AddSingleton<ConfigurationService>(provider => new ConfigurationService(dbPath));
            services.AddSingleton<IConfigurationService>(provider => provider.GetRequiredService<ConfigurationService>());
            services.AddSingleton<IConfigurationRepository>(provider => provider.GetRequiredService<ConfigurationService>());

            services.AddSingleton<FileSystemStatisticsService>(provider =>
            {
                var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FileSystemStatisticsService>>();
                return new FileSystemStatisticsService(logger, dbPath);
            });
            services.AddSingleton<IReportingService>(provider => provider.GetRequiredService<FileSystemStatisticsService>());
            services.AddSingleton<IEmailStatisticsRepository>(provider => provider.GetRequiredService<FileSystemStatisticsService>());

            services.AddSingleton<IEmailStatisticsExporter, ExcelEmailStatisticsExporter>();
            services.AddSingleton<IAttachmentStorageService, FileSystemAttachmentStorageService>();

            services.AddSingleton<GraphAuthFactory>();
            services.AddSingleton<IGraphClient, GraphClient>();

            return services;
        }

        private static string ResolveDatabasePath(IConfiguration configuration)
        {
            var configuredPath = configuration["Persistence:ConfigurationDbPath"];
            var candidatePath = string.IsNullOrWhiteSpace(configuredPath)
                ? "mailmonitor.db"
                : configuredPath.Trim();

            var absolutePath = Path.IsPathRooted(candidatePath)
                ? candidatePath
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, candidatePath));

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return absolutePath;
        }
    }
}
