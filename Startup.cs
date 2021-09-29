using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

[assembly: FunctionsStartup(typeof(AzFuncQueueDemo.Startup))]

namespace AzFuncQueueDemo
{
    public class Startup : FunctionsStartup
    {
        //Defines the OpenTelemetry resource attribute "service.name" which is mandatory
        private const string servicename = "AzFuncQueueDemo";

        //Defines the OpenTelemetry Instrumentation Library.
        private const string activitySource = "OpenTelemetryDemo.AzFuncQueueDemo";
  
        public override void Configure(IFunctionsHostBuilder builder)
        {
            //Using insecure channel: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md#special-case-when-using-insecure-channel
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            
            builder.Services.AddSingleton<ActivitySource>((s) => {
                return new ActivitySource(activitySource);
            });

            //Do not use AddOpenTelemetryTracing (https://github.com/open-telemetry/opentelemetry-dotnet/issues/1803#issuecomment-800608308)
            builder.Services.AddSingleton((builder) =>
            {
                return Sdk.CreateTracerProviderBuilder()
                    .AddSource(activitySource)
                    .SetSampler(new AlwaysOnSampler())
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(servicename))
                    //.AddHttpClientInstrumentation() doesn't work:  https://github.com/Azure/azure-functions-host/issues/7135
                    .AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(Environment.GetEnvironmentVariable("CollectorUrl") ?? "http://localhost:55680");
                    }).Build();
            });
            
        }
    }
}
