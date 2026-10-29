using System;
using System.Linq;
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
            var topic = configuration["TOPICS:SETTINGS"];
            if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Settings topic must be configured.");
            await Task.Run(() =>
            {
                var config = KafkaCreator.GetClientConfig(configuration);
                if (config.SecurityProtocol is not (SecurityProtocol.Ssl or SecurityProtocol.SaslSsl))
                    throw new InvalidOperationException("External Kafka transport requires TLS.");
                config.EnableSslCertificateVerification = true;
                config.SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https;
                using var client = factory is null ? new AdminClientBuilder(config)
                    .SetLogHandler((_, _) => { }).SetErrorHandler((_, _) => { }).Build() : factory(config);
                // An all-topics request cannot implicitly create a named topic.
                var metadata = client.GetMetadata(TimeSpan.FromSeconds(3));
                var settings = metadata.Topics.SingleOrDefault(item => item.Topic == topic);
                if (settings is null)
                    throw new KafkaException(new Error(ErrorCode.UnknownTopicOrPart));
                if (settings.Error.IsError) throw new KafkaException(settings.Error);
                if (settings.Partitions.Count == 0) throw new KafkaException(new Error(ErrorCode.UnknownTopicOrPart));
                var failed = settings.Partitions.FirstOrDefault(item => item.Error.IsError);
                if (failed is not null) throw new KafkaException(failed.Error);
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
