using System;
using System.Threading;
using System.Threading.Tasks;
using Cassandra;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Coflnet.Sky.Settings.Infrastructure;

internal static class DatabaseReadiness
{
    public static Task CheckAsync(IServiceProvider services, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            var session = services.GetRequiredService<ISession>();
            var redis = services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
            await Task.WhenAll(
                session.ExecuteAsync(new SimpleStatement("SELECT key FROM system.local").SetReadTimeoutMillis(3000)),
                redis.PingAsync());
        }, cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
}
