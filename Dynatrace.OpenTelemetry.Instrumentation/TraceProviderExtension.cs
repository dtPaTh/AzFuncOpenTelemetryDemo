using Dynatrace.OpenTelemetry.Instrumentation.Implementation;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Trace;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Dynatrace.OpenTelemetry
{
    public static class TraceProviderExtension
    {
        public static TracerProviderBuilder AddDynatraceExporter(this TracerProviderBuilder builder)
        {
            var otlpEndpoint = new Uri(Environment.GetEnvironmentVariable("OTLPEndpoint") ?? "http://localhost:55681");

            if (otlpEndpoint.Scheme == "http")
            {
                //Using insecure channel: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#special-case-when-using-insecure-channel
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            builder.AddOtlpExporter((otlpOptions) => {
                otlpOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                otlpOptions.Endpoint = otlpEndpoint;
                var dtApiToken = Environment.GetEnvironmentVariable("DT_API_TOKEN");
                if (!String.IsNullOrEmpty(dtApiToken))
                    otlpOptions.Headers = "Authorization=Api-Token " + Environment.GetEnvironmentVariable("DT_API_TOKEN");
            });

            return builder;

        }

        public static TracerProviderBuilder AddTraceMessageHandlerInstrumentation(this TracerProviderBuilder builder)
        {
            builder.AddSource(InstrumentationConstants.HttpClientActivitySource);
            return builder;
        }

        public static TracerProviderBuilder AddServiceBusSenderInstrumentation(this TracerProviderBuilder builder)
        {
            builder.AddSource(InstrumentationConstants.ServiceBusActivitySource);
            return builder;
        }
    }
}