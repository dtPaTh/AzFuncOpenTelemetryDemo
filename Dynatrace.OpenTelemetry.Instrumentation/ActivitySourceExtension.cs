using Dynatrace.OpenTelemetry.Instrumentation.Implementation;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Dynatrace.OpenTelemetry.Instrumentation.Http
{
    public static class ActivitySourceExtension
    {
        public static Activity? StartActivity(this ActivitySource source, string name, ActivityKind kind = ActivityKind.Internal, HttpRequest req= null)
        {
            if (req != null)
            {
                var ctx = Propagators.DefaultTextMapPropagator.Extract(default, req, HttpRequestContextPropagation.HeaderValuesGetter);
                return source.StartActivity(name, kind, parentContext:ctx.ActivityContext);
            }
            else
                return source.StartActivity(name, kind);
        }
    }
}