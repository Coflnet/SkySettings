using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using System;
using System.Linq;
using StackExchange.Redis;
using System.Collections.Generic;
using Cassandra.Data.Linq;
using ISession = Cassandra.ISession;
using System.Diagnostics;
using Prometheus;

namespace Coflnet.Sky.Settings.Services
{
    public class StorageService : ISettingsService, IDisposable
    {
        ISession _session;
        private ConnectionMultiplexer connection;
        private static Prometheus.Counter settingsUpdate = Prometheus.Metrics.CreateCounter("sky_settings_update", "How many updates were processed");
        private static readonly Histogram queryDuration = Metrics.CreateHistogram(
            "sky_settings_storage_duration_seconds", "Time spent executing settings storage operations",
            new HistogramConfiguration { LabelNames = ["operation"], Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) });
        public StorageService(ISession session, ConnectionMultiplexer connection)
        {
            _session = session;
            this.connection = connection;
        }

        public async Task<ISession> GetSession(string keyspace = "settings")
        {
            return _session;
        }

        public async Task<string> GetSetting(string userId, string settingKey)
        {
            using var activity = SettingsTelemetry.ActivitySource.StartActivity("cassandra.settings.get", ActivityKind.Client);
            activity?.SetTag("db.system", "cassandra");
            activity?.SetTag("settings.key", settingKey);
            using var timer = queryDuration.WithLabels("get").NewTimer();
            var table = await GetTable();
            var result = await table.Where(s => s.UserId == userId && s.Key == settingKey).Select(s => s.Value).ExecuteAsync();
            var value = result.FirstOrDefault();
            activity?.SetTag("settings.found", value != null);
            return value;
        }

        private async Task<Table<CassandraSetting>> GetTable(ISession session = null)
        {
            session ??= await GetSession();
            return new Table<CassandraSetting>(session);
        }

        public async Task<IEnumerable<Setting>> GetSettings(string userId, List<string> keys)
        {
            using var activity = SettingsTelemetry.ActivitySource.StartActivity("cassandra.settings.get_many", ActivityKind.Client);
            activity?.SetTag("db.system", "cassandra");
            activity?.SetTag("settings.key_count", keys.Count);
            using var timer = queryDuration.WithLabels("get_many").NewTimer();
            var table = await GetTable();
            var result = await table.Where(s => s.UserId == userId && keys.Contains(s.Key)).ExecuteAsync();
            return result.Select(s => new Setting() { Key = s.Key, Value = s.Value });
        }

        public async Task UpdateSetting(string userId, string settingKey, string newValue)
        {
            var table = await GetTable();
            if(newValue == null)
            {
                await table.Where(s => s.UserId == userId && s.Key == settingKey).Delete().ExecuteAsync();
                return;
            }
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

        public async Task DeleteUser(string userId)
        {
            var table = await GetTable();
            await table.Where(s => s.UserId == userId).Delete().ExecuteAsync();
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
