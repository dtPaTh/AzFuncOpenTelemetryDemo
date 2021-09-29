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

namespace OpenTelemetryDemo
{
    public class AzFuncQueueDemo
    {

        //env-var to pick Queue ConnectionString
        private const string envSBConnectionStr = "SBConnection";
        private const string QueueName = "workitems";

        //message property used by servicebus client sdk to pass trace context
        private const string contextProperty = "Diagnostic-Id";

        ActivitySource _activitySource;
        TracerProvider _traceProvider;

        public AzFuncQueueDemo(ActivitySource activitySource, TracerProvider traceProvider)
        {
            _activitySource = activitySource;
            _traceProvider = traceProvider;
        }

        [FunctionName("TimerTrigger")]
        public async Task RunTrigger([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log)
        {

            //create root-span
            using (var activity = _activitySource.StartActivity("Trigger", ActivityKind.Server))
            {

                var client = new HttpClient();

                //HttpClient requires custom context propagation: https://github.com/Azure/azure-functions-host/issues/7135
                if (activity != null)
                    client.DefaultRequestHeaders.Add("traceparent", activity.Id);

                var targetUrl = Environment.GetEnvironmentVariable("WebTriggerUrl") ?? "http://localhost:7071/api/WebTrigger";

                var res = await client.GetAsync(targetUrl);

                if (res.IsSuccessStatusCode)
                    log.LogInformation("Successfully called WebTrigger");
                else
                    log.LogWarning("Failed calling WebTrigger");

            }

        }


        [FunctionName("WebTrigger")]
        public async Task<IActionResult> RunWebTrigger([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log)
        {
            using (var activity = _activitySource.StartActivity("WebTrigger", ActivityKind.Producer, req.Headers["traceparent"]))
            {
                //follow semantic conventions for messaging: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md
                activity?.AddTag("peer.service", "ServiceBus");
                activity?.AddTag("messaging.system", "ServiceBus");
                activity?.AddTag("messaging.destination", QueueName);
                activity?.AddTag("messaging.destination_kind", "queue");

                var client = new ServiceBusClient(Environment.GetEnvironmentVariable(envSBConnectionStr));

                //client framework automatically tags message. no need for custom propagation
                var sender = client.CreateSender(QueueName);

                // Create a new message to send to the queue
                var message = new ServiceBusMessage($"Message {DateTime.Now}");
                log.LogInformation($"Sending message: {message}");

                // Send the message to the queue
                await sender.SendMessageAsync(message);
            }

            return new OkObjectResult("Ok");
     
        }
        
        
        [FunctionName("Consumer")]
        public async Task RunConsumer([ServiceBusTrigger(QueueName, Connection = envSBConnectionStr)] Message myQueueItem, ILogger log)
        {
            string tracecontext="";
            if (myQueueItem.UserProperties.ContainsKey(contextProperty))
            {
                tracecontext = myQueueItem.UserProperties[contextProperty] as string;
                log.LogInformation($"C# Queue trigger function processed: {myQueueItem} with tracecontext '{tracecontext}'");
            }
            else
                log.LogInformation($"C# Queue trigger function processed: {myQueueItem} without tracecontext");

            //create root-span, connecting with trace-parent read from a message property
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
                    var client = new HttpClient();
                    //HttpClient requires custom context propagation: https://github.com/Azure/azure-functions-host/issues/7135
                    if (activity != null)
                        client.DefaultRequestHeaders.Add("traceparent", activity.Id);

                    var res = await client.GetAsync(outboundServiceUrl);
                    if (res.IsSuccessStatusCode)
                        log.LogInformation("Successfully called outbound Service");
                    else
                        activity.AddEvent(new ActivityEvent("Unable to call outbound service. Service returned an error"));
                }
                   
            }

        }
    }
}
