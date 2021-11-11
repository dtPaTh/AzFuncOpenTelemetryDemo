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