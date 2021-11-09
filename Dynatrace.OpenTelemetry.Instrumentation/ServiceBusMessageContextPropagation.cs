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
        public static Func<Message, string, IEnumerable<string>> MessagePropertiesGetter => (msg, name) =>
        {
            const string contextProperty = "Diagnostic-Id";
            const string correlationContextProperty = "Correlation-Context";

            //map W3C-Tracecontext to properties used by ServiceBusClient
            if (name == "traceparent")
                name = contextProperty;
            else if (name == "tracestate")
                name = correlationContextProperty;
            else 
                return null;

            
            if (msg.UserProperties.ContainsKey(contextProperty))
                return new StringValues(msg.UserProperties[contextProperty] as string);

            return null;
        };
    }
}