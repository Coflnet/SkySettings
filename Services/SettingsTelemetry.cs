using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Coflnet.Sky.Settings.Services;

internal static class SettingsTelemetry
{
    public const string ActivitySourceName = "Coflnet.Sky.Settings";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Registers the settings activity source on the tracer provider configured by AddCoflnetCore
    /// (which already owns instrumentation, sampling and the OTLP exporter).
    /// </summary>
    public static void AddSettingsActivitySource(this IServiceCollection services)
    {
        services.ConfigureOpenTelemetryTracerProvider(builder => builder.AddSource(ActivitySourceName));
    }
}
