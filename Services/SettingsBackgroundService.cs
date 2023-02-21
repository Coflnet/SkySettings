using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using System;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Settings.Controllers;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Serialization;
using System.Linq;
using System.Collections.Generic;

namespace Coflnet.Sky.Settings.Services
{
    /// <summary>
    /// Backround service handling migrations and queue consuming
    /// </summary>
    public class SettingsBackgroundService : BackgroundService
    {
        private IServiceScopeFactory scopeFactory;
        private IConfiguration config;
        private ILogger<SettingsBackgroundService> logger;

        /// <summary>
        /// Creates a new instance of <see cref="SettingsBackgroundService"/>
        /// </summary>
        /// <param name="scopeFactory"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public SettingsBackgroundService(
            IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<SettingsBackgroundService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.config = config;
            this.logger = logger;
        }
        /// <summary>
        /// Called by asp.net on startup
        /// </summary>
        /// <param name="stoppingToken">is canceled when the applications stops</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var toMigrate = new List<string>();
            using (var scope = scopeFactory.CreateScope())
            {
                using var context = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<StorageService>();
                var exists = await service.GetSetting("0", "migrated");

                if (exists == null)
                {
                    toMigrate = await context.Users.Select(u => u.ExternalId).AsNoTracking().ToListAsync();
                    // iterate over all settings 

                }
            }
            using (var scope = scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<StorageService>();
                foreach (var item in toMigrate)
                {
                    // iterate over all settings of the user
                    using (var innerscope = scopeFactory.CreateScope())
                    using (var innerDb = innerscope.ServiceProvider.GetRequiredService<SettingsDbContext>())
                        foreach (var setting in await innerDb.Settings.Where(s => s.User.ExternalId == item).AsNoTracking().ToListAsync())
                        {
                            // update the setting in the storage
                            await service.UpdateSetting(item, setting.Key, setting.Value);
                        }
                    logger.LogInformation($"applied settings for {item} to storage");
                }
                await service.UpdateSetting("0", "migrated", "true");
                logger.LogInformation("applied all settings to storage");
            }

            var flipCons = Coflnet.Kafka.KafkaConsumer.Consume<SettingsUpdate>(config["KAFKA_HOST"], config["TOPICS:SETTINGS"], async setting =>
            {
                var service = GetService();
                await service.UpdateSetting(setting.UserId, setting.Key, setting.Value);
            }, stoppingToken, "flipbase");
            logger.LogInformation("applied all migrations");

            await Task.WhenAll(flipCons);
        }


        private SettingsService GetService()
        {
            return scopeFactory.CreateScope().ServiceProvider.GetRequiredService<SettingsService>();
        }
    }
}