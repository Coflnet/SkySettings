using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Cassandra;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace Coflnet.Sky.Settings.Infrastructure;

public class DatabaseReadinessTests
{
    private Mock<ISession> session;
    private Mock<IDatabase> redis;
    private ServiceProvider services;

    [SetUp]
    public void Setup()
    {
        session = new Mock<ISession>();
        session.Setup(s => s.ExecuteAsync(It.IsAny<IStatement>())).ReturnsAsync(new RowSet());
        redis = new Mock<IDatabase>();
        redis.Setup(r => r.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.Zero);
        var connection = new Mock<IConnectionMultiplexer>();
        connection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redis.Object);
        services = new ServiceCollection().AddSingleton(session.Object).AddSingleton(connection.Object).BuildServiceProvider();
    }

    [TearDown]
    public void Cleanup() => services.Dispose();

    [Test]
    public async Task EveryReadinessCheckPerformsFreshCqlAndRedisOperations()
    {
        await DatabaseReadiness.CheckAsync(services);
        await DatabaseReadiness.CheckAsync(services);
        session.Verify(s => s.ExecuteAsync(It.Is<SimpleStatement>(q => q.QueryString == "SELECT key FROM system.local")), Times.Exactly(2));
        redis.Verify(r => r.PingAsync(It.IsAny<CommandFlags>()), Times.Exactly(2));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EitherUnavailableDependencyFailsReadiness(bool cql)
    {
        if (cql) session.Setup(s => s.ExecuteAsync(It.IsAny<IStatement>())).ThrowsAsync(new InvalidOperationException("synthetic CQL failure"));
        else redis.Setup(r => r.PingAsync(It.IsAny<CommandFlags>())).ThrowsAsync(new InvalidOperationException("synthetic Redis failure"));
        Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseReadiness.CheckAsync(services));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StalledDependencyHasOneFiveSecondOverallDeadline(bool cql)
    {
        if (cql) session.Setup(s => s.ExecuteAsync(It.IsAny<IStatement>())).Returns(new TaskCompletionSource<RowSet>().Task);
        else redis.Setup(r => r.PingAsync(It.IsAny<CommandFlags>())).Returns(new TaskCompletionSource<TimeSpan>().Task);
        var elapsed = Stopwatch.StartNew();
        Assert.ThrowsAsync<TimeoutException>(() => DatabaseReadiness.CheckAsync(services));
        Assert.That(elapsed.Elapsed, Is.InRange(TimeSpan.FromSeconds(4.9), TimeSpan.FromSeconds(7)));
    }

    [Test]
    public void StartupConnectionConstructionIsInsideTheDeadline()
    {
        using var unavailable = new ServiceCollection().AddSingleton<ISession>(_ =>
            throw new InvalidOperationException("synthetic startup connection failure")).BuildServiceProvider();
        Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseReadiness.CheckAsync(unavailable));
    }
}
