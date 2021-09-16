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

namespace OpenTelemetryDemo
{
    public static class AzFuncQueueDemo
    {
        //env-var to pick url to call web-triggered function 
        private const string envWebTriggerUrl = "WebTriggerUrl";

        //env-var to pick Queue ConnectionString
        private const string envSBConnectionStr = "SBConnection";

        //Defines the OpenTelemetry resource attribute "service.name" which is mandatory
        private const string servicename = "AzFuncQueueDemo";

        //Defines the OpenTelemetry Instrumentation Library.
        private const string activitySource = "OpenTelemetryDemo.AzFuncQueueDemo";

        //Provides the API for starting/stopping activities.
        private static readonly ActivitySource myActivitySource = new ActivitySource(activitySource);

        //message property used by servicebus client sdk to pass context
        private const string contextProperty = "Diagnostic-Id";


        private const string QueueName = "workitems";
        //private const string QueueName = "workitems_svc";

        private static string CollectorUrl
        {
            get { return Environment.GetEnvironmentVariable("COLLECTOR_URL") ?? "http://localhost:55680"; }
        }

        [FunctionName("TimerTrigger")]
        public static async Task RunTrigger([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            using (Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(activitySource)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(servicename))
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(CollectorUrl);
            })
            .Build())
            {
                //create root-span
                using (var activity = myActivitySource.StartActivity("Trigger", ActivityKind.Server))
                {

                    var client = new HttpClient();

                    //should be done automatically with .AddHttpClientInstrumentation(),
                    //but doesn't work in Azure Consumption Plan.
                    //Tested with Host Runtime 3.1 and 3.2
                    client.DefaultRequestHeaders.Add("traceparent",activity.Id);

                    var targetUrl = Environment.GetEnvironmentVariable(envWebTriggerUrl) ??"http://localhost:7071/api/WebTrigger";
                    
                    var res = await client.GetAsync(targetUrl);
                    
                    if (res.IsSuccessStatusCode)
                        log.LogInformation("Successfully called WebTrigger");
                    else
                        log.LogWarning("Failed calling WebTrigger");
                    
                }
            }

        }


        [FunctionName("WebTrigger")]
        public static async Task<IActionResult> RunWebTrigger([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log)
        {

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            using (Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddSource(activitySource)
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(servicename))
            .AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(CollectorUrl);
            })
            .Build())
            {
                
                //create root-span, connecting with trace-parent read from the http-header
                using ( myActivitySource.StartActivity("WebTrigger", ActivityKind.Server, req.Headers["traceparent"]))
                {
                    
                    using (var activity = myActivitySource.StartActivity("Producer", ActivityKind.Producer))
                    {
                        //follow semantic conventions for messaging
                        activity.AddTag("peer.service", "ServiceBus");
                        activity.AddTag("messaging.system", "ServiceBus");
                        activity.AddTag("messaging.destination", QueueName);
                        activity.AddTag("messaging.destination_kind", "queue");

                        var client = new ServiceBusClient(Environment.GetEnvironmentVariable(envSBConnectionStr));

                        //client framework automatically tags message. no need for custom propagation
                        var sender = client.CreateSender(QueueName);

                        // Create a new message to send to the queue
                        var message = new ServiceBusMessage($"Message {DateTime.Now}");
                        log.LogInformation($"Sending message: {message}");

                        // Send the message to the queue
                        await sender.SendMessageAsync(message);
                    }
                    
                }
            }


            return new OkObjectResult("Ok");
         
        
     
        }

        
        
        [FunctionName("Consumer")]
        public static async Task RunConsumer([ServiceBusTrigger(QueueName, Connection = envSBConnectionStr)] Message myQueueItem, ILogger log)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            using (Sdk.CreateTracerProviderBuilder()
             .SetSampler(new AlwaysOnSampler())
             .AddSource(activitySource)
             .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(servicename))
            .AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(CollectorUrl);
            })
            .Build())
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
                using (var activity = myActivitySource.StartActivity("Consumer", ActivityKind.Consumer, tracecontext))
                {
                    //follow semantic conventions for messaging
                    activity.AddTag("peer.service", "ServiceBus");
                    activity.AddTag("messaging.system", "ServiceBus");
                    activity.AddTag("messaging.destination", QueueName);
                    activity.AddTag("messaging.destination_kind", "queue");

                    log.LogInformation("Do something");
                    await Task.Delay(200);
                   
                }

            }

        }
    }
}
