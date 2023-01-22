using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using System;
using System.Linq;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Coflnet.Sky.Settings.Services
{
    public class SettingsService
    {
        private SettingsDbContext db;
        private ConnectionMultiplexer connection;
        private static Prometheus.Counter settingsUpdate = Prometheus.Metrics.CreateCounter("sky_settings_update", "How many updates were processed");

        public SettingsService(SettingsDbContext db, ConnectionMultiplexer connection)
        {
            this.db = db;
            this.connection = connection;
        }

        internal async Task UpdateSetting(string userId, string settingKey, string newValue)
        {
            //using var trans = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var user = await GetOrCreateUser(userId);
            await AddOrUpdateSetting(user, settingKey, newValue);
            await db.SaveChangesAsync();
            settingsUpdate.Inc();
        }

        private async Task<User> GetOrCreateUser(string userId)
        {
            var user = await db.Users.Where(u => u.ExternalId == userId)
                            .Include(u => u.Settings).FirstOrDefaultAsync();

            if (user == null)
            {
                user = new User() { ExternalId = userId };
                user.Settings = new System.Collections.Generic.HashSet<Setting>();
                db.Add(user);
            }

            return user;
        }

        private async Task AddOrUpdateSetting(User user, string settingKey, string newValue)
        {
            var setting = user.Settings.Where(s => s.Key == settingKey).FirstOrDefault();
            if (setting == null)
            {
                setting = new Setting() { Key = settingKey, Value = newValue };
                user.Settings.Add(setting);
            }
            else
            {
                if (setting.Value == newValue)
                    return; // nothing changed
                setting.Value = newValue;
                setting.ChangeIndex++;
                db.Update(setting);
            }
            var pubsub = connection.GetSubscriber();
            await pubsub.PublishAsync(user.ExternalId + settingKey, newValue);
        }

        internal async Task UpdateSettings(string userId, List<Setting> settings)
        {
            var user = await GetOrCreateUser(userId);
            foreach (var item in settings)
            {
                await AddOrUpdateSetting(user, item.Key, item.Value);
            }
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Gets a single setting
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="settingKey"></param>
        /// <returns></returns>
        public async Task<string> GetSetting(string userId, string settingKey)
        {
            return (await GetSettings(userId, new List<string>() { settingKey }))?.FirstOrDefault()?.Value;
        }


        /// <summary>
        /// Get multiple settings
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Setting>> GetSettings(string userId, List<string> keys)
        {
            return (await db.Users
                .Where(u => u.ExternalId == userId)
                .Select(u => new { settings = u.Settings.Where(s => keys.Contains(s.Key)) })
                .FirstOrDefaultAsync())?.settings;
        }
    }
}
