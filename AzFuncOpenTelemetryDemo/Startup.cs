using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Dynatrace.OpenTelemetry;
using Dynatrace.OpenTelemetry.Instrumentation.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

[assembly: FunctionsStartup(typeof(AzFuncQueueDemo.Startup))]

namespace AzFuncQueueDemo
{

    
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            //Defines the OpenTelemetry instrumentation library
            string activitySource = Environment.GetEnvironmentVariable("otel.instrumetnationlibary")??"Custom";

            //Do not use builder.Services.AddOpenTelemetryTracing (https://github.com/open-telemetry/opentelemetry-dotnet/issues/1803#issuecomment-800608308)
            builder.Services.AddSingleton((builder) =>
            {
                return Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddDynatraceExporter()
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(Environment.GetEnvironmentVariable("otel.service.name") ??"defaultservice"))
                    //.AddHttpClientInstrumentation() doesn't work:  https://github.com/Azure/azure-functions-host/issues/7135 ...
                    //..instead use an alternative instrumentation not relying on DiagnosticListener
                    .AddTraceMessageHandlerInstrumentation()  //Requires to use TraceMessageHandler as a DelegationHandler for HttpClient (registered below)
                    .AddServiceBusSenderInstrumentation()  //Requires to use TracedServiceBusSender vs ServiceBusSender
                    .AddSource(activitySource) //register activitysource used for custom instrumentation
                    .Build();
            });

            //Register TraceMessageHandler and a httpclient using it. 
            builder.Services.AddTransient<TraceMessageHandler>();
            builder.Services.AddHttpClient("traced-client")
                .AddHttpMessageHandler<TraceMessageHandler>();

            //For easier handling, use a typed httpclient https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
            //available from Dynatrace.OpenTelemetry.Instrumentation (optional) 
            builder.Services.AddTransient<TracedHttpClient>();

            //For easier handling, DI ActivitySource (optional)         
            builder.Services.AddSingleton<ActivitySource>((s) => {
                return new ActivitySource(activitySource);
            });


        }
    }
}
