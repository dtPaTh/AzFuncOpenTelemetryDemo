# Trace Azure Functions

Tracing Azure Functions (runtime <= v3) for .NET Core    can be tricky due to limitations of the Azure Function runtime: 
* https://github.com/Azure/azure-functions-host/issues/7135 Unable to use auto-instrumentation (e.g. HttpClient, SQLClien) provided within .NET framework
* https://github.com/open-telemetry/opentelemetry-dotnet/issues/1803#issuecomment-800608308 Unable to intialize Opentelemetry using AddOpenTelemetryTracing extension method
**Note** 

The following sample demonstrates end-2-end traceability using OpenTelemetry. 

## The Demo-Setup
[TimerTriggerdFunction] -> (http) -> [HttpTriggeredFunction] -> (ServiceBusQueue) -> [ServiceBusTriggeredFunction] -> (http) -> [Outbound Service]

### Requirements
* Azure ServiceBus Queue, configure connectionstring in config parameter "SBConnection"; Queuename is "workitems"
* An OTLP/GRPC capable endpoint to receive exported spans. The endpoint is configured via config parameter "OTLPEndpoint".
* If published to Azure, configure Url of the HttpTriggeredFunction to be called by the TimerTriggeredFunction, otherwise a local endpoint is used.
* [Optional] Configure a Url called after message is received

## Trace Instrumentation
OpenTelemetry is initialized using DependencyInjection within the FunctionStartup in Startup.cs. The instrumentation of the Functions is using the .NET Activity API. Fore mor details about 
instrumentations read here: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Api/README.md#instrumenting-a-libraryapplication-with-net-activity-api

While the .NET framework provides a broad set of [auto-instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet) for e.g. Sqlclient or Asp.Net Core - due to the current 
limitations developers need to take care of instrumentation and context-propagation. 

A minimum instrumentation is applied by creatig root-spans within every function and passing the trace-context where needed (e.g. incoming/outgoing http calls).

# Sending traces to Dynatrace
Dynatrace supports ingestion of traces using the OTLP/HTTP format. To ingest traces exported from instrumented application using the OTLP/GRPC, you have to use an OpenTelemetry colllector. 

See following instructions to activate the Dynatrace OTLP endpoint: https://www.dynatrace.com/support/help/how-to-use-dynatrace/transactions-and-services/purepath-distributed-traces/opentelemetry-ingest/#activate

## Running the collector as a docker container
OpenTelemetry provides a docker image for the collector. To use this image a collector config needs to be applied. 

Within this project a sample collector config is included (otel_collector_config.yaml) which enables OTLP receivers and a OLTP/HTTP sender adding authentication headers. The target endpoint and authentication token is configured through environment variables.  
```
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:55680
      http:
        endpoint: 0.0.0.0:55681
exporters:
  otlphttp:
    endpoint: "${DT_OTLPHTTP_ENDPOINT}"
    headers: {"Authorization": "Api-Token ${DT_API_TOKEN}"}
  logging:
    loglevel: debug
    sampling_initial: 5
    sampling_thereafter: 200
service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: []
      exporters: [logging,otlphttp]
```
### Run a collector as a docker locally for testing

Linux
```
docker run -p 55680:55680 -p 55681:55681 -e DT_OTLPHTTP_ENDPOINT="<YOUR-DYNATRACE-OTLP-ENDPOINT>" -e DT_API_TOKEN="<YOUR-DYNATRACE-API-TOKEN>" -v  $(pwd)/otel_collector_config.yaml:/etc/otel/config.yaml otel/opentelemetry-collector-contrib
```

Windows Powershell
```
docker run -p 55680:55680 -p 55681:55681 -e DT_OTLPHTTP_ENDPOINT="<YOUR-DYNATRACE-OTLP-ENDPOINT>" -e DT_API_TOKEN="<YOUR-DYNATRACE-API-TOKEN>" -v  ${pwd}/otel_collector_config.yaml:/etc/otel/config.yaml otel/opentelemetry-collector-contrib
```

Your command then may look similar like this if your are using a Dynatrace SaaS endpoint. 
```
docker run -p 55680:55680 -p 55681:55681 -e DT_OTLPHTTP_ENDPOINT="https://xxxxxxxx.live.dynatrace.com/api/v2/otlp" -e DT_API_TOKEN="xxxxxx.xxxxxxxxxxxxxxxxxxxxxxxx.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" -v  $(pwd)/otel_collector_config.yaml:/etc/otel/config.yaml otel/opentelemetry-collector-contrib
```
### Build a docker image for you collector, you can run in your container platform of choice

Within this project a sample dockerfile is included (otel_collector.Dockerfile) packaging the collector config "otel_collector_config.yaml"

1. Create a docker image to include your OpenTelemetry collector config
```
docker build -t dt-otlp-collector . -f otel_collector.Dockerfile
```











