using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Settings.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Coflnet.Kafka;
using StackExchange.Redis;
using Cassandra;

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
        private KafkaConsumer consumer;

        /// <summary>
        /// Creates a new instance of <see cref="SettingsBackgroundService"/>
        /// </summary>
        /// <param name="scopeFactory"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public SettingsBackgroundService(
            IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<SettingsBackgroundService> logger, KafkaConsumer consumer,
            /*Warm start*/ ConnectionMultiplexer redis, ISession cassandra)
        {
            this.scopeFactory = scopeFactory;
            this.config = config;
            this.logger = logger;
            this.consumer = consumer;
        }
        /// <summary>
        /// Called by asp.net on startup
        /// </summary>
        /// <param name="stoppingToken">is canceled when the applications stops</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var flipCons = consumer.Consume<SettingsUpdate>(config["TOPICS:SETTINGS"], async setting =>
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