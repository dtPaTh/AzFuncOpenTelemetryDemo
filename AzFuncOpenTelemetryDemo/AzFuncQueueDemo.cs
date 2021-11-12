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
using OpenTelemetry.Trace;
using Dynatrace.OpenTelemetry.Instrumentation;
using Dynatrace.OpenTelemetry.Instrumentation.Http;
using Dynatrace.OpenTelemetry.Instrumentation.ServiceBus;

namespace OpenTelemetryDemo
{
    /// <summary>
    /// Azure Function Demo
    /// 
    /// Note: Comments marked with //Instrumentation: highlight the added instrumentation for tracing 
    /// using Dynatrace.OpenTelemetry.Instrumentation as an alternative to the native Instrumentation provided 
    /// which is not working due to missing DiagnosticLinsteners in Azure Functions (https://github.com/Azure/azure-functions-host/issues/7135)
    /// 
    /// </summary>
    public class AzFuncQueueDemo
    {

        //env-var to pick Queue ConnectionString
        private const string envSBConnectionStr = "SBConnection";
        private const string QueueName = "workitems";


        //Instrumentation: Dependency Injection
        //ActivitySource registered with TraceProvider for custom instrumentation
        private ActivitySource _activitySource;

        //Instrumented httpclient
        private HttpClient _httpClient;
        
        //Instrumentation: TraceProvider is mandatory to be present in context for the actibitysource
        //ActivitySource and TracedHttpClient are DI for easier handling tor educe boilerplate code.
        public AzFuncQueueDemo(ActivitySource activitySource, TracerProvider traceProvider, TracedHttpClient client)
        {
            _activitySource = activitySource;
            _httpClient = client.Client;
        }

        [FunctionName("TimerTrigger")]
        public async Task RunTrigger([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log)
        {
            //Instrumentation: create root-span
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
            //Instrumentation: create root-span using extension method provided in Dynatrace.OpenTelemetry.Instrumentation,
            //that automatically propagates tracecontext from incoming httprequest object
            using (var activity = _activitySource.StartActivity("WebTrigger", ActivityKind.Server, req)) 
            {
                var client = new ServiceBusClient(Environment.GetEnvironmentVariable(envSBConnectionStr));
                
                //Instrumentation: use an instrumented version of the ServiceBusSender
                var sender = new TracedServiceBusSender(client.CreateSender(QueueName));
          
                await sender.SendMessageAsync(new ServiceBusMessage($"Message {DateTime.Now}"));
                
            }

            return new OkObjectResult("Ok");
        }
        
        
        [FunctionName("Consumer")]
        public async Task RunConsumer([ServiceBusTrigger(QueueName, Connection = envSBConnectionStr)] Message myQueueItem, ILogger log)
        {
            //Instrumentation: create root-span using extension method provided in Dynatrace.OpenTelemetry.Instrumentation,
            //that automatically propagates tracecontext from incoming message object
            using (var activity = _activitySource.StartActivity("Consumer", ActivityKind.Consumer, QueueName, myQueueItem))
            {
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
