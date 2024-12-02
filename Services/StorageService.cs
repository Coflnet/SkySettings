using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using System;
using System.Linq;
using StackExchange.Redis;
using System.Collections.Generic;
using Cassandra.Data.Linq;
using ISession = Cassandra.ISession;

namespace Coflnet.Sky.Settings.Services
{
    public class StorageService : ISettingsService, IDisposable
    {
        ISession _session;
        private ConnectionMultiplexer connection;
        private static Prometheus.Counter settingsUpdate = Prometheus.Metrics.CreateCounter("sky_settings_update", "How many updates were processed");
        public StorageService(ISession session, ConnectionMultiplexer connection)
        {
            this._session = session;
            this.connection = connection;
        }

        public async Task<ISession> GetSession(string keyspace = "settings")
        {
            return _session;
        }

        public async Task<string> GetSetting(string userId, string settingKey)
        {
            var table = await GetTable();
            var result = await table.Where(s => s.UserId == userId && s.Key == settingKey).Select(s => s.Value).ExecuteAsync();
            return result.FirstOrDefault();
        }

        private async Task<Table<CassandraSetting>> GetTable(ISession session = null)
        {
            if (session == null)
                session = await GetSession();
            return new Table<CassandraSetting>(session);
        }

        public async Task<IEnumerable<Setting>> GetSettings(string userId, List<string> keys)
        {
            var table = await GetTable();
            var result = await table.Where(s => s.UserId == userId && keys.Contains(s.Key)).ExecuteAsync();
            return result.Select(s => new Setting() { Key = s.Key, Value = s.Value });
        }

        public async Task UpdateSetting(string userId, string settingKey, string newValue)
        {
            var table = await GetTable();
            var setting = (await table.Where(s => s.UserId == userId && s.Key == settingKey).ExecuteAsync()).FirstOrDefault();
            if (setting != null && setting.Value == newValue)
                return; // nothing changed (optimization
            var nextId = 1L;
            if (setting != null)
            {
                nextId = setting.ChangeIndex + 1;
            }
            connection.GetSubscriber().Publish(userId + settingKey, newValue);
            setting = new CassandraSetting() { UserId = userId, Key = settingKey, Value = newValue, LastUpdate = DateTime.UtcNow, ChangeIndex = nextId };
            await table.Insert(setting).ExecuteAsync();
        }

        public async Task UpdateSettings(string userId, List<Setting> settings)
        {
            foreach (var item in settings)
            {
                await UpdateSetting(userId, item.Key, item.Value);
            }
        }

        internal async Task Init()
        {
            await GetSession();
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
