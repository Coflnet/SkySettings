using System;
using System.Threading.Tasks;
using Coflnet.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace Coflnet.Sky.Settings.Infrastructure;

internal static class KafkaReadiness
{
    // Startup only: preserve the consumer's existing endpoint, file credentials and SASL settings.
    public static async Task CheckAsync(IConfiguration configuration, Func<AdminClientConfig, IAdminClient> factory = null)
    {
        try
        {
            await Task.Run(() =>
            {
                var config = KafkaCreator.GetClientConfig(configuration);
                if (string.IsNullOrWhiteSpace(config.BootstrapServers))
                    throw new ArgumentException("Kafka brokers must be configured.");
                if (config.SecurityProtocol is not (SecurityProtocol.Ssl or SecurityProtocol.SaslSsl))
                    throw new InvalidOperationException("External Kafka transport requires TLS.");
                config.EnableSslCertificateVerification = true;
                config.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
                using var client = factory is null ? new AdminClientBuilder(config)
                    .SetLogHandler((_, _) => { }).SetErrorHandler((_, _) => { }).Build() : factory(config);
                // An all-topics request cannot implicitly create a named topic.
                var metadata = client.GetMetadata(TimeSpan.FromSeconds(3));
                if (metadata.Brokers.Count == 0)
                    throw new KafkaException(new Error(ErrorCode.Local_AllBrokersDown));
            }).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (KafkaException error)
        {
            throw new InvalidOperationException("Kafka startup metadata check failed (" + error.Error.Code + ").");
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("Kafka startup metadata check failed (" + error.GetType().Name + ").");
        }
    }
}
