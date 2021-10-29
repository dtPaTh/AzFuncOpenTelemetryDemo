using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace OpenTelemetry.Instrumentation.Http
{
    public static class HttpRequestContextPropagation
    {
        public static Func<HttpRequest, string, IEnumerable<string>> HeaderValuesGetter => (request, name) =>
        {
            if (request.Headers.TryGetValue(name, out var values))
            {
                return values;
            }

            return null;
        };
    }
}