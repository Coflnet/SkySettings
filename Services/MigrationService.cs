using System.Threading.Tasks;
using System;
using System.Linq;
using StackExchange.Redis;
using Cassandra.Data.Linq;
using Coflnet.Cassandra;
using Microsoft.Extensions.Logging;
using ISession = Cassandra.ISession;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.Settings.Models;
namespace Coflnet.Sky.Settings.Services;

public class MigrationService : BackgroundService
{
    private ISession session;
    private ISession oldSession;
    private ILogger<MigrationService> logger;
    private ConnectionMultiplexer redis;
    // get di
    private IServiceProvider serviceProvider;
    private IConfiguration config;
    public bool IsDone { get; private set; }

    public MigrationService(ISession session, OldSession oldSession, ILogger<MigrationService> logger, ConnectionMultiplexer redis, IServiceProvider serviceProvider, IConfiguration config)
    {
        this.session = session;
        this.logger = logger;
        this.redis = redis;
        this.serviceProvider = serviceProvider;
        this.oldSession = oldSession.Session;
        this.config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var handlerLogger = serviceProvider.GetRequiredService<ILogger<MigrationHandler<CassandraSetting, CassandraSetting>>>();
        var minutehandler = new MigrationHandler<CassandraSetting, CassandraSetting>(
                () => new Table<CassandraSetting>(oldSession),
                session, handlerLogger, redis,
                () => new Table<CassandraSetting>(session),
                a =>
                {
                    if (a.Key.Contains("mod"))
                    {
                        logger.LogInformation("skipping mod key");
                        return null;

                    }
                    return a;
                });
        await minutehandler.Migrate();
        logger.LogInformation("Migrated, starting to replay kafka");

    }
}