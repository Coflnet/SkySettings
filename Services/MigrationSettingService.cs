using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using System;
using System.Linq;
using StackExchange.Redis;
using System.Collections.Generic;
using Cassandra;
using Cassandra.Data.Linq;
using Coflnet.Cassandra;
using Microsoft.Extensions.Logging;
using ISession = Cassandra.ISession;
namespace Coflnet.Sky.Settings.Services;
/// <summary>
/// Used while/to migrate settings from the old storage to the new one
/// </summary>
public class MigrationSettingService : ISettingsService
{
    private Table<CassandraSetting> table;
    private Table<CassandraSetting> oldTable;
    private ConnectionMultiplexer connection;
    private ILogger<MigrationSettingService> logger;

    public MigrationSettingService(ISession session, OldSession oldSession, ConnectionMultiplexer connection, ILogger<MigrationSettingService> logger)
    {
        this.table = new Table<CassandraSetting>(session);
        this.oldTable = new Table<CassandraSetting>(oldSession.Session);
        table.CreateIfNotExistsAsync();
        this.connection = connection;
        this.logger = logger;
    }

    public async Task<string> GetSetting(string userId, string settingKey)
    {
        var resultTask = table.Where(s => s.UserId == userId && s.Key == settingKey).Select(s => s.Value).ExecuteAsync();
        var oldResult = (await oldTable.Where(s => s.UserId == userId && s.Key == settingKey).Select(s => s.Value).ExecuteAsync()).FirstOrDefault();
        var result = (await resultTask).FirstOrDefault();
        if (result != null && result != oldResult)
        {
            logger.LogInformation($"Migrating setting {settingKey} for user {userId} is different");
        }
        return result ?? oldResult;
    }

    public async Task<IEnumerable<Setting>> GetSettings(string userId, List<string> keys)
    {
        var oldResultTask = oldTable.Where(s => s.UserId == userId && keys.Contains(s.Key)).ExecuteAsync();
        var result = await table.Where(s => s.UserId == userId && keys.Contains(s.Key)).ExecuteAsync();
        var oldResult = await oldResultTask;
        if (result.Count() < oldResult.Count())
        {
            return oldResult.Select(s => new Setting() { Key = s.Key, Value = s.Value });
        }
        return result.Select(s => new Setting() { Key = s.Key, Value = s.Value });
    }

    public async Task UpdateSetting(string userId, string settingKey, string newValue)
    {
        var setting = (await table.Where(s => s.UserId == userId && s.Key == settingKey).ExecuteAsync()).FirstOrDefault();
        var settingOld = (await oldTable.Where(s => s.UserId == userId && s.Key == settingKey).ExecuteAsync()).FirstOrDefault();
        if (setting == null && settingOld != null)
        {
            setting = settingOld;
        }
        if (setting != null && setting.Value == newValue)
            return;
        var nextId = 1L;
        if (setting != null)
            nextId = setting.ChangeIndex + 1;
        connection.GetSubscriber().Publish(userId + settingKey, newValue);
        setting = new CassandraSetting() { UserId = userId, Key = settingKey, Value = newValue, LastUpdate = DateTime.UtcNow, ChangeIndex = nextId };
        await table.Insert(setting).ExecuteAsync();
        await oldTable.Insert(setting).ExecuteAsync();
    }

    public async Task UpdateSettings(string userId, List<Setting> settings)
    {
        foreach (var item in settings)
        {
            await UpdateSetting(userId, item.Key, item.Value);
        }
    }
}

