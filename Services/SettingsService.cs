using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using System.Collections.Generic;

namespace Coflnet.Sky.Settings.Services
{
    public interface ISettingsService
    {
        Task<string> GetSetting(string userId, string settingKey);
        Task<IEnumerable<Setting>> GetSettings(string userId, List<string> keys);
        Task UpdateSetting(string userId, string settingKey, string newValue);
        Task UpdateSettings(string userId, List<Setting> settings);
    }
}
