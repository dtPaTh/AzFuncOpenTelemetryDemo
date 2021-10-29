using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using AzFuncQueueDemo;
using Dynatrace.OpenTelemetry.Instrumentation.Http;

namespace OpenTelemetryDemo
{
    public class AzFuncQueueDemo
    {

        //env-var to pick Queue ConnectionString
        private const string envSBConnectionStr = "SBConnection";
        private const string QueueName = "workitems";

        //message property used by servicebus client sdk to pass trace context: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-end-to-end-tracing?tabs=net-standard-sdk-2
        private const string contextProperty = "Diagnostic-Id";

        //Dependency Injected:
        //ActivitySource registered with TraceProvider for custom instrumentation
        private ActivitySource _activitySource;
        //Instrumented httpclient
        private HttpClient _httpClient;

        //TraceProvider is mandatory to be present in context for the actibitysource
        public AzFuncQueueDemo(ActivitySource activitySource, TracerProvider traceProvider, TracedHttpClient client)
        {
            _activitySource = activitySource;
            _httpClient = client.Client;
        }

        [FunctionName("TimerTrigger")]
        public async Task RunTrigger([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log)
        {
            //create root-span
            using (var activity = _activitySource.StartActivity("Trigger", ActivityKind.Server))
            {
                var res = await _httpClient.GetAsync(Environment.GetEnvironmentVariable("WebTriggerUrl") ?? "http://localhost:7071/api/WebTrigger");

                if (!res.IsSuccessStatusCode)
                    log.LogWarning("Failed calling WebTrigger");
            }
        }


        [FunctionName("WebTrigger")]
        public async Task<IActionResult> RunWebTrigger([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log)
        {
            //create root-span using extension method provided in Dynatrace.OpenTelemetry.Instruemntation,
            //that automatically propagates tracecontext from incoming httprequest object
            using (var activity = _activitySource.StartActivity("WebTrigger", ActivityKind.Producer, req)) 
            {
                //follow semantic conventions for messaging: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md
                activity?.AddTag("peer.service", "ServiceBus");
                activity?.AddTag("messaging.system", "ServiceBus");
                activity?.AddTag("messaging.destination", QueueName);
                activity?.AddTag("messaging.destination_kind", "queue");

                var client = new ServiceBusClient(Environment.GetEnvironmentVariable(envSBConnectionStr));

                //client framework automatically does context-propagation on messages https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-end-to-end-tracing?tabs=net-standard-sdk-2
                var sender = client.CreateSender(QueueName);
                await sender.SendMessageAsync(new ServiceBusMessage($"Message {DateTime.Now}"));
            }

            return new OkObjectResult("Ok");
        }
        
        
        [FunctionName("Consumer")]
        public async Task RunConsumer([ServiceBusTrigger(QueueName, Connection = envSBConnectionStr)] Message myQueueItem, ILogger log)
        {
            //read tracecontext from message payload: https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-end-to-end-tracing?tabs=net-standard-sdk-2
            string tracecontext ="";
            if (myQueueItem.UserProperties.ContainsKey(contextProperty))
                tracecontext = myQueueItem.UserProperties[contextProperty] as string;

            //create root-span, setting tracecontext 
            using (var activity = _activitySource.StartActivity("Consumer", ActivityKind.Consumer, tracecontext))
            {
                //follow semantic conventions for messaging: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md
                activity?.AddTag("peer.service", "ServiceBus");
                activity?.AddTag("messaging.system", "ServiceBus");
                activity?.AddTag("messaging.destination", QueueName);
                activity?.AddTag("messaging.destination_kind", "queue");

                //call another service which is instrumented with OneAgent
                var outboundServiceUrl = Environment.GetEnvironmentVariable("OutboundServiceUrl");
                if (!String.IsNullOrEmpty(outboundServiceUrl))
                {
                    var res = await _httpClient.GetAsync(outboundServiceUrl);
                    if (!res.IsSuccessStatusCode)
                        log.LogInformation("Failed calling outbound Service");
                }
                   
            }

        }
    }
}
