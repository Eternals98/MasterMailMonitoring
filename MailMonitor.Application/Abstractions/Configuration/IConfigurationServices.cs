using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Jobs;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Domain.Entities.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMonitor.Application.Abstractions.Configuration
{
    public interface IConfigurationService
    {
        // Configuración global
        Task<Setting> GetSettingsAsync();
        Task UpdateSettingsAsync(Setting setting);

        // Empresas
        Task<List<Company>> GetCompaniesAsync();
        Task<Company?> GetCompanyByIdAsync(Guid id);
        Task<Result> AddOrUpdateCompanyAsync(Company company);
        Task<Result> DeleteCompanyAsync(Guid id);

        // Triggers
        Task<List<Trigger>> GetTriggersAsync();
        Task<Trigger?> GetTriggerByIdAsync(Guid id);
        Task<Result> AddOrUpdateTriggerAsync(Trigger trigger);
        Task<Result> DeleteTriggerAsync(Guid id);

        // Graph Settings
        Task<GraphSetting?> GetGraphSettingsAsync();
        Task<Result> UpdateGraphSettingsAsync(GraphSetting settings);
    }
}
