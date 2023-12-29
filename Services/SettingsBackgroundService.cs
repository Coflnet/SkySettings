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
        private StorageService storageService;

        /// <summary>
        /// Creates a new instance of <see cref="SettingsBackgroundService"/>
        /// </summary>
        /// <param name="scopeFactory"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public SettingsBackgroundService(
            IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<SettingsBackgroundService> logger, StorageService storageService)
        {
            this.scopeFactory = scopeFactory;
            this.config = config;
            this.logger = logger;
            this.storageService = storageService;
        }
        /// <summary>
        /// Called by asp.net on startup
        /// </summary>
        /// <param name="stoppingToken">is canceled when the applications stops</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var flipCons = Coflnet.Kafka.KafkaConsumer.Consume<SettingsUpdate>(config, config["TOPICS:SETTINGS"], async setting =>
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                await service.UpdateSetting(setting.UserId, setting.Key, setting.Value);
            }, stoppingToken, "flipbase");
            logger.LogInformation("applied all migrations");

            await Task.WhenAll(flipCons);
        }
    }
}