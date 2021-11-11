using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Dynatrace.OpenTelemetry.Instrumentation.Http
{
    public class TracedHttpClient
    {
        public HttpClient Client { get; }
        public TracedHttpClient(IHttpClientFactory clientFactory)
        {
            Client = clientFactory.CreateClient("traced-client");
        }
    }
}
