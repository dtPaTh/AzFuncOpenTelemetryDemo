using Azure.Messaging.ServiceBus;
using Dynatrace.OpenTelemetry.Instrumentation.Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.ServiceBus;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;


namespace Dynatrace.OpenTelemetry.Instrumentation
{
    public static class ActivitySourceExtension
    {
        public static Activity StartActivity(this ActivitySource source, string name, ActivityKind kind = ActivityKind.Internal, HttpRequest req= null)
        {
            if (req != null)
            {
                var ctx = Propagators.DefaultTextMapPropagator.Extract(default, req, HttpRequestContextPropagation.HeaderValuesGetter);
                return source.StartActivity(name, kind, parentContext:ctx.ActivityContext);
            }
            else
                return source.StartActivity(name, kind);
        }


        public static Activity StartActivity(this ActivitySource source, string name, ServiceBusSender sender)
        {
            Activity actitivy = source.StartActivity(name, ActivityKind.Producer);

            if (actitivy != null)
            {
                //follow semantic conventions for messaging: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md
                actitivy.AddTag("peer.service", "ServiceBus");
                actitivy.AddTag("messaging.system", "ServiceBus");
                actitivy.AddTag("messaging.destination_kind", "queue");
                if (sender != null)
                {
                    actitivy.AddTag("messaging.destination", sender.EntityPath);
                    actitivy.AddTag("net.peer.name", sender.FullyQualifiedNamespace);
                }
                
            }

            return actitivy;
        }
        public static Activity StartActivity(this ActivitySource source, string name, ActivityKind kind, string queueName, Message msg)
        {
            Activity actitivy = null;
            if (msg != null)
            {
                var ctx = Propagators.DefaultTextMapPropagator.Extract(default, msg, ServiceBusMessageContextPropagation.MessagePropertiesGetter);
                actitivy = source.StartActivity(name, kind, parentContext: ctx.ActivityContext);
            }
            else
                actitivy =  source.StartActivity(name, kind);

            if (actitivy != null)
            {
                //follow semantic conventions for messaging: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md
                actitivy.AddTag("peer.service", "ServiceBus");
                actitivy.AddTag("messaging.system", "ServiceBus");
                actitivy.AddTag("messaging.destination", queueName);
                actitivy.AddTag("messaging.destination_kind", "queue");
            }


            return actitivy;

        }

    }
}