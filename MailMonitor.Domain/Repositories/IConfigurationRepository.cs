using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Graph;
using MailMonitor.Domain.Entities.Jobs;
using MailMonitor.Domain.Entities.Settings;

namespace MailMonitor.Domain.Repositories
{
    public interface IConfigurationRepository
    {
        Task<Setting> GetSettingsAsync();
        Task UpdateSettingsAsync(Setting setting);

        Task<List<Company>> GetCompaniesAsync();
        Task<Company?> GetCompanyByIdAsync(Guid id);
        Task<Result> AddOrUpdateCompanyAsync(Company company);
        Task<Result> DeleteCompanyAsync(Guid id);

        Task<List<Trigger>> GetTriggersAsync();
        Task<Trigger?> GetTriggerByIdAsync(Guid id);
        Task<Result> AddOrUpdateTriggerAsync(Trigger trigger);
        Task<Result> DeleteTriggerAsync(Guid id);

        Task<GraphSetting?> GetGraphSettingsAsync();
        Task<Result> UpdateGraphSettingsAsync(GraphSetting settings);
    }
}
