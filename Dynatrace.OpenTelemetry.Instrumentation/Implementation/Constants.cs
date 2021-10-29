using System;
using System.Collections.Generic;
using System.Text;

namespace Dynatrace.OpenTelemetry.Instrumentation.Implementation
{
    public static class InstrumentationConstants
    {
        public const string ActivitySourceName = "Dynatrace.OpenTelemetry.Instrumentation.";

        public const string HttpClientActivitySource = ActivitySourceName + "HttpClient";
    }
}
