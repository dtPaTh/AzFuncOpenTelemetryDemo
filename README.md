# Trace Azure Functions

Tracing Azure Functions (runtime <= v3) for .NET Core (in-process model) can be tricky due to limitations of the Azure Function runtime: 

**Azure Functions runtime limitations:**
* https://github.com/Azure/azure-functions-host/issues/7135
Unable to use auto-instrumentation (e.g. HttpClient, Ado.Net) provided within .NET framework
* https://github.com/open-telemetry/opentelemetry-dotnet/issues/1803#issuecomment-800608308
Unable to intialize Opentelemetry using AddOpenTelemetryTracing extension method

The following sample demonstrates end-2-end traceability using OpenTelemetry. 

## The Demo-Setup
[TimerTriggerdFunction] -> (http) -> [HttpTriggeredFunction] -> (ServiceBusQueue) -> [ServiceBusTriggeredFunction] -> (http) -> [Outbound Service]

## Requirements
* Azure ServiceBus Queue, configure connectionstring in config parameter "SBConenction"; Queuename is "workitems"
* An OTLP (grcp) capable endpoint to receive exported spans. The endpoint is configured via config parameter "CollectorUrl". Optional use of an OpenTelemetry Collector to further process/forward the telemetry.
* If published to Azure, configure Url of the HttpTriggeredFunction to be called by the TimerTriggeredFunction, otherwise local endpoint is used.
* [Optional] Configure a Url called after message is received





