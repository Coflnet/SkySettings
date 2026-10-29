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
    private static IConfiguration Configuration(string topic = "synthetic-settings") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["TOPICS:SETTINGS"] = topic, ["KAFKA:BROKERS"] = "broker.invalid:9093",
            ["KAFKA:USERNAME"] = "synthetic-user", ["KAFKA:PASSWORD"] = "synthetic-password",
            ["KAFKA:TLS:CA_LOCATION"] = "synthetic-ca.pem",
            ["KAFKA:TLS:CERTIFICATE_LOCATION"] = "synthetic-client.pem", ["KAFKA:TLS:KEY_LOCATION"] = "synthetic-client.key"
        }).Build();

    private static Metadata Metadata(string topic = "synthetic-settings", ErrorCode error = ErrorCode.NoError) =>
        new Metadata(new List<BrokerMetadata>(), new List<TopicMetadata>
        {
            new TopicMetadata(topic, new List<PartitionMetadata>
            {
                new PartitionMetadata(0, 1, new[] { 1 }, new[] { 1 }, new Error(ErrorCode.NoError))
            }, new Error(error))
        }, 1, "synthetic-broker");

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
    public void MissingTopicConfigurationDoesNotConstructANativeClient()
    {
        var factory = new Mock<Func<AdminClientConfig, IAdminClient>>();
        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(Configuration(null), factory.Object));
        Assert.That(failure.Message, Is.EqualTo("Kafka startup metadata check failed (ArgumentException)."));
        factory.Verify(f => f(It.IsAny<AdminClientConfig>()), Times.Never);
    }

    [TestCase("another-topic", ErrorCode.NoError)]
    [TestCase("synthetic-settings", ErrorCode.TopicAuthorizationFailed)]
    public void MissingOrUnauthorizedTopicPreventsStartup(string topic, ErrorCode error)
    {
        var client = new Mock<IAdminClient>();
        client.Setup(c => c.GetMetadata(TimeSpan.FromSeconds(3))).Returns(Metadata(topic, error));
        Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(Configuration(), _ => client.Object));
        client.Verify(c => c.GetMetadata(TimeSpan.FromSeconds(3)), Times.Once);
        client.Verify(c => c.Dispose(), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Test]
    public void NativeFailureExposesOnlyCodeAndNeverPrivateReasonOrInnerException()
    {
        var client = new Mock<IAdminClient>();
        client.Setup(c => c.GetMetadata(It.IsAny<TimeSpan>()))
            .Throws(new KafkaException(new Error(ErrorCode.Local_Authentication, "synthetic-private-native-reason")));
        var failure = Assert.ThrowsAsync<InvalidOperationException>(() => KafkaReadiness.CheckAsync(Configuration(), _ => client.Object));
        Assert.That(failure.Message, Is.EqualTo("Kafka startup metadata check failed (Local_Authentication)."));
        Assert.That(failure.InnerException, Is.Null);
        Assert.That(failure.ToString(), Does.Not.Contain("synthetic-private-native-reason"));
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
