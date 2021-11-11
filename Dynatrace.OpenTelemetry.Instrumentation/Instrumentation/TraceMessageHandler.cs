using Dynatrace.OpenTelemetry.Instrumentation.Implementation;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Http.Implementation;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Dynatrace.OpenTelemetry.Instrumentation.Http
{
    public class TraceMessageHandler : DelegatingHandler
    {
        ActivitySource _activitySource = new ActivitySource(InstrumentationConstants.HttpClientActivitySource);
        
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {

            using (Activity activity = _activitySource.StartActivity(HttpTagHelper.GetOperationNameForHttpMethod(request.Method)))
            {
                // Propagate context
                Propagators.DefaultTextMapPropagator.Inject(new PropagationContext(activity.Context, Baggage.Current), request, HttpRequestMessageContextPropagation.HeaderValueSetter);

                activity.SetTag("Http.Host", HttpTagHelper.GetHostTagValueFromRequestUri(request.RequestUri));
                activity.SetTag("Http.Url", HttpTagHelper.GetUriTagValueFromRequestUri(request.RequestUri));
                //activity?.SetTag("Http.Flavor", HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.Version)); //optional in origin instrumentation

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}