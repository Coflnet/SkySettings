using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace Coflnet.Sky.Settings.Infrastructure;

public class KafkaReadinessTests
{
    private static IConfiguration Configuration(string brokers = "broker.invalid:9093") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["KAFKA:BROKERS"] = brokers,
            ["KAFKA:USERNAME"] = "synthetic-user", ["KAFKA:PASSWORD"] = "synthetic-password",
            ["KAFKA:TLS:CA_LOCATION"] = "synthetic-ca.pem",
            ["KAFKA:TLS:CERTIFICATE_LOCATION"] = "synthetic-client.pem", ["KAFKA:TLS:KEY_LOCATION"] = "synthetic-client.key"
        }).Build();

    private static Metadata Metadata(bool hasBroker = true, List<TopicMetadata> topics = null) =>
        new Metadata(hasBroker ? new List<BrokerMetadata> { new BrokerMetadata(1, "synthetic-broker", 9093) }
            : new List<BrokerMetadata>(), topics ?? new List<TopicMetadata>(), 1, "synthetic-broker");

    [Test]
    public async Task StartupUsesExistingAccountAndTlsFilesWithoutANameBearingMetadataRequest()
    {
        var client = new Mock<IAdminClient>();
        client.Setup(c => c.GetMetadata(TimeSpan.FromSeconds(3))).Returns(Metadata());
        await KafkaReadiness.CheckAsync(Configuration(), config =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(config.BootstrapServers, Is.EqualTo("broker.invalid:9093"));
                Assert.That(config.SaslUsername, Is.EqualTo("synthetic-user"));
                Assert.That(config.SaslPassword, Is.EqualTo("synthetic-password"));
                Assert.That(config.SecurityProtocol, Is.EqualTo(SecurityProtocol.SaslSsl));
                Assert.That(config.SaslMechanism, Is.EqualTo(SaslMechanism.ScramSha256));
                Assert.That(config.SslCaLocation, Is.EqualTo("synthetic-ca.pem"));
                Assert.That(config.SslCertificateLocation, Is.EqualTo("synthetic-client.pem"));
                Assert.That(config.SslKeyLocation, Is.EqualTo("synthetic-client.key"));
                Assert.That(config.EnableSslCertificateVerification, Is.True);
                Assert.That(config.SslEndpointIdentificationAlgorithm, Is.EqualTo(SslEndpointIdentificationAlgorithm.Https));
            });
            return client.Object;
        });
        client.Verify(c => c.GetMetadata(TimeSpan.FromSeconds(3)), Times.Once);
        client.Verify(c => c.Dispose(), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Test]
    public void MissingTlsKeyCannotSilentlySelectPlaintextTransport()
    {
        var configuration = Configuration();
        configuration["KAFKA:TLS:KEY_LOCATION"] = null;
        var factory = new Mock<Func<AdminClientConfig, IAdminClient>>();
        Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(configuration, factory.Object));
        factory.Verify(f => f(It.IsAny<AdminClientConfig>()), Times.Never);
    }

    [Test]
    public void MissingBrokerConfigurationDoesNotConstructANativeClient()
    {
        var factory = new Mock<Func<AdminClientConfig, IAdminClient>>();
        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(Configuration(null), factory.Object));
        Assert.That(failure.Message, Is.EqualTo("Kafka startup metadata check failed (ArgumentException)."));
        factory.Verify(f => f(It.IsAny<AdminClientConfig>()), Times.Never);
    }

    [Test]
    public void ResponseWithoutAnyBrokerPreventsStartup()
    {
        var client = new Mock<IAdminClient>();
        client.Setup(c => c.GetMetadata(TimeSpan.FromSeconds(3))).Returns(Metadata(hasBroker: false));
        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(Configuration(), _ => client.Object));
        Assert.That(failure.Message, Is.EqualTo("Kafka startup metadata check failed (Local_AllBrokersDown)."));
        client.Verify(c => c.GetMetadata(TimeSpan.FromSeconds(3)), Times.Once);
        client.Verify(c => c.Dispose(), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Test]
    public async Task TopicAuthorizationIsSeparateFromSuccessfulBrokerTransport()
    {
        var client = new Mock<IAdminClient>();
        client.Setup(c => c.GetMetadata(TimeSpan.FromSeconds(3))).Returns(Metadata(topics: new List<TopicMetadata>
        {
            new TopicMetadata("unrelated-hidden-topic", new List<PartitionMetadata>(), new Error(ErrorCode.TopicAuthorizationFailed))
        }));
        await KafkaReadiness.CheckAsync(Configuration(), _ => client.Object);
        client.Verify(c => c.GetMetadata(TimeSpan.FromSeconds(3)), Times.Once);
        client.Verify(c => c.Dispose(), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [TestCase(ErrorCode.Local_Authentication)]
    [TestCase(ErrorCode.Local_Transport)]
    public void NativeFailureExposesOnlyCodeAndNeverPrivateReasonOrInnerException(ErrorCode code)
    {
        var client = new Mock<IAdminClient>();
        client.Setup(c => c.GetMetadata(It.IsAny<TimeSpan>()))
            .Throws(new KafkaException(new Error(code, "synthetic-private-native-reason")));
        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(Configuration(), _ => client.Object));
        Assert.That(failure.Message, Is.EqualTo("Kafka startup metadata check failed (" + code + ")."));
        Assert.That(failure.InnerException, Is.Null);
        Assert.That(failure.ToString(), Does.Not.Contain("synthetic-private-native-reason"));
        client.Verify(c => c.Dispose(), Times.Once);
    }

    [Test]
    public void EntireClientConstructionAndMetadataCallHaveAFiveSecondDeadline()
    {
        using var release = new ManualResetEventSlim();
        var finished = new TaskCompletionSource();
        var client = new Mock<IAdminClient>();
        client.Setup(c => c.GetMetadata(It.IsAny<TimeSpan>())).Returns(Metadata());
        client.Setup(c => c.Dispose()).Callback(() => finished.SetResult());
        var elapsed = Stopwatch.StartNew();
        try
        {
            var failure = Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(Configuration(), _ =>
            {
                release.Wait();
                return client.Object;
            }));
            Assert.That(failure.Message, Is.EqualTo("Kafka startup metadata check failed (TimeoutException)."));
            Assert.That(elapsed.Elapsed, Is.InRange(TimeSpan.FromSeconds(4.9), TimeSpan.FromSeconds(7)));
        }
        finally
        {
            release.Set();
            Assert.That(finished.Task.Wait(TimeSpan.FromSeconds(2)), Is.True);
        }
    }
}
