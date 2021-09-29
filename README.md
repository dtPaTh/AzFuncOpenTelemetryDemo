# The Demo-Setup
[TimerTriggerdFunction] -> (http) -> [HttpTriggeredFunction] -> (ServiceBusQueue) -> [ServiceBusTriggeredFunction]

# Requirements
* Azure ServiceBus Queue, configure connectionstring in config parameter "SBConenction"; Queuename is "workitems"
* OpenTelemetry Collector, configure endpoint in config parameter "COLLECTOR_URL"
  * Collector needs to accept OTLP (grpc) and configured as desired.
* If published to Azure, configure Url of the HttpTriggeredFunction to be called by the TimerTriggeredFunction, otherwise local endpoint is used.





