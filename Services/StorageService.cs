using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using System;
using System.Linq;
using StackExchange.Redis;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Cassandra;
using Cassandra.Data.Linq;

namespace Coflnet.Sky.Settings.Services
{
    public class StorageService : ISettingsService,IDisposable
    {
        IConfiguration config;
        Cassandra.ISession _session;
        private ConnectionMultiplexer connection;
        private static Prometheus.Counter settingsUpdate = Prometheus.Metrics.CreateCounter("sky_settings_update", "How many updates were processed");
        public StorageService(IConfiguration config, ConnectionMultiplexer connection)
        {
            this.config = config;
            this.connection = connection;
        }

        public async Task<Cassandra.ISession> GetSession(string keyspace = "settings")
        {
            if (_session != null)
                return _session;
            var cluster = Cluster.Builder()
                                .WithCredentials(config["CASSANDRA:USER"], config["CASSANDRA:PASSWORD"])
                                .AddContactPoints(config["CASSANDRA:HOSTS"]?.Split(",") ?? throw new System.Exception("No ASSANDRA:HOSTS defined in config"))
                                .WithDefaultKeyspace(keyspace)
                                .Build();
            if (keyspace == null)
                return await cluster.ConnectAsync();
            var replication = new Dictionary<string, string>()
            {
                {"class", config["CASSANDRA:REPLICATION_CLASS"]},
                {"replication_factor", config["CASSANDRA:REPLICATION_FACTOR"]}
            };
            _session = cluster.ConnectAndCreateDefaultKeyspaceIfNotExists(replication);

            await (await GetTable(_session)).CreateIfNotExistsAsync();
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
            if(setting != null && setting.Value == newValue)
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
            var table = await GetTable();
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
