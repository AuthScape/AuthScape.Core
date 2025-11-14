using AuthScape.Models.Settings;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IDP.Services.IdentityServer
{
    public interface ISettingsService
    {
        Task<List<Settings>> GetAllSettingsAsync();
        Task<Settings> GetSettingAsync(string name);
        Task<string> GetSettingValueAsync(string name);
        Task<bool> GetSettingValueAsBoolAsync(string name);
        Task UpdateSettingAsync(string name, string value);
        Task CreateSettingAsync(string name, string value, int settingTypeId = 0);
        Task DeleteSettingAsync(string name);
    }
}
