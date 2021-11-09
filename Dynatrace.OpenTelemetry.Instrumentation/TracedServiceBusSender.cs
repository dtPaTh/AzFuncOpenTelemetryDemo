using Azure.Messaging.ServiceBus;
using Dynatrace.OpenTelemetry.Instrumentation.Implementation;
using Dynatrace.OpenTelemetry.Instrumentation;
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


namespace Dynatrace.OpenTelemetry.Instrumentation.ServiceBus
{
    public class TracedServiceBusSender : ServiceBusSender
    {
        ActivitySource _activitySource = new ActivitySource(InstrumentationConstants.ServiceBusActivitySource);

        ServiceBusSender _sender;

        public TracedServiceBusSender(ServiceBusSender sender)
        {
            _sender = sender;
        }

        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            using (var activity = _activitySource.StartActivity("SendMessageAsync", _sender))
            {
                Propagators.DefaultTextMapPropagator.Inject(new PropagationContext(activity.Context, Baggage.Current), message, ServiceBusMessageContextPropagation.MessagePropertiesSetter);
                return _sender.SendMessageAsync(message, cancellationToken);
            }
        }

    }
}