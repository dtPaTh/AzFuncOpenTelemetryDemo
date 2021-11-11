using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Net.Http;


namespace OpenTelemetry.Instrumentation.Http
{
    public static class ServiceBusMessageContextPropagation
    {
        //https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-end-to-end-tracing?tabs=net-standard-sdk
        const string contextProperty = "Diagnostic-Id"; //available in Azure.Messaging.ServiceBus as well as Microsoft.Azure.ServiceBus SDK
        const string correlationContextProperty = "Correlation-Context"; //only used in (old) Microsoft.Azure.ServiceBus SDKhttps://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-end-to-end-tracing?tabs=net-standard-sdk

        public static Func<Message, string, IEnumerable<string>> MessagePropertiesGetter => (msg, name) =>
        {

            //map W3C-TraceContext to properties used by ServiceBus SDKs
            if (name == "traceparent")
                name = contextProperty;
            else if (name == "tracestate")
                name = correlationContextProperty;
            else 
                return null;
            
            if (msg.UserProperties.ContainsKey(name))
                return new StringValues(msg.UserProperties[name] as string);

            return null;
        };

        public static Action<ServiceBusMessage, string, string> MessagePropertiesSetter => (msg, name, value) =>
        {
            //map W3C-TraceContext to properties used by ServiceBus SDKs
            if (name == "traceparent" && !msg.ApplicationProperties.ContainsKey(contextProperty))
                msg.ApplicationProperties[contextProperty] = value;
            else if (name == "tracestate" && !msg.ApplicationProperties.ContainsKey(correlationContextProperty)) //Enhance with tracestate if not already set
                msg.ApplicationProperties[correlationContextProperty] = value;
        };
    }
}