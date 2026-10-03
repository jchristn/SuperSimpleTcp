namespace SuperSimpleTcp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;

    internal static class SimpleTcpInstrumentation
    {
        internal static readonly string Version = GetVersion();

        internal static readonly Meter Meter = new Meter(SimpleTcpTelemetryNames.MeterName, Version);

        internal static readonly ActivitySource ActivitySource = new ActivitySource(SimpleTcpTelemetryNames.ActivitySourceName, Version);

        internal static readonly UpDownCounter<long> ConnectionsActive = Meter.CreateUpDownCounter<long>(
            SimpleTcpTelemetryNames.ConnectionsActive, "{connection}", "Connections currently established.");

        internal static readonly UpDownCounter<long> ConnectionsLimit = Meter.CreateUpDownCounter<long>(
            SimpleTcpTelemetryNames.ConnectionsLimit, "{connection}", "Configured connection capacity (MaxConnections) of listening servers.");

        internal static readonly Counter<long> ConnectionsOpened = Meter.CreateCounter<long>(
            SimpleTcpTelemetryNames.ConnectionsOpened, "{connection}", "Connections successfully established.");

        internal static readonly Counter<long> ConnectionsClosed = Meter.CreateCounter<long>(
            SimpleTcpTelemetryNames.ConnectionsClosed, "{connection}", "Connections closed, by reason.");

        internal static readonly Counter<long> ConnectionsRejected = Meter.CreateCounter<long>(
            SimpleTcpTelemetryNames.ConnectionsRejected, "{connection}", "Inbound connections rejected by the server, by reason.");

        internal static readonly Histogram<double> ConnectionDuration = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.ConnectionDuration, "s", "Connection lifetime from establishment to close.");

        internal static readonly UpDownCounter<long> ListenersActive = Meter.CreateUpDownCounter<long>(
            SimpleTcpTelemetryNames.ListenersActive, "{listener}", "Server listeners currently accepting connections.");

        internal static readonly Histogram<double> AcceptDuration = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.AcceptDuration, "s", "Server time from socket accept to connection ready or rejected.");

        internal static readonly Histogram<double> TlsHandshakeDuration = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.TlsHandshakeDuration, "s", "TLS handshake duration.");

        internal static readonly Histogram<double> ConnectDuration = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.ConnectDuration, "s", "Duration of a single client connection attempt.");

        internal static readonly Histogram<double> SendDuration = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.SendDuration, "s", "Send duration including lock wait, write, and flush.");

        internal static readonly Histogram<double> SendLockWaitDuration = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.SendLockWaitDuration, "s", "Time spent waiting for the per-connection send lock.");

        internal static readonly Counter<long> SentBytes = Meter.CreateCounter<long>(
            SimpleTcpTelemetryNames.SentBytes, "By", "Bytes written to the network.");

        internal static readonly Counter<long> ReceivedBytes = Meter.CreateCounter<long>(
            SimpleTcpTelemetryNames.ReceivedBytes, "By", "Bytes read from the network.");

        internal static readonly Histogram<long> ReceiveSegmentSize = Meter.CreateHistogram<long>(
            SimpleTcpTelemetryNames.ReceiveSegmentSize, "By", "Size of each segment read from the network.");

        internal static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.HandlerDuration, "s", "Execution time of the DataReceived handler.");

        internal static readonly UpDownCounter<long> DispatchQueueDepth = Meter.CreateUpDownCounter<long>(
            SimpleTcpTelemetryNames.DispatchQueueDepth, "{segment}", "Received segments awaiting asynchronous DataReceived dispatch.");

        internal static readonly Histogram<double> DispatchQueueWait = Meter.CreateHistogram<double>(
            SimpleTcpTelemetryNames.DispatchQueueWait, "s", "Time a received segment waited in the asynchronous dispatch queue.");

        internal static readonly Counter<long> MonitorRuns = Meter.CreateCounter<long>(
            SimpleTcpTelemetryNames.MonitorRuns, "{run}", "Background monitor evaluations, by monitor and outcome.");

        internal static readonly Counter<long> Errors = Meter.CreateCounter<long>(
            SimpleTcpTelemetryNames.Errors, "{error}", "Errors by operation and exception type.");

        internal static readonly ObservableGauge<int> BuildInfo = Meter.CreateObservableGauge<int>(
            SimpleTcpTelemetryNames.BuildInfo,
            () => new Measurement<int>(1, new KeyValuePair<string, object>(SimpleTcpTelemetryNames.AttributeVersion, Version)),
            "{info}",
            "Always 1; carries the SuperSimpleTcp library version as a label.");

        private static string GetVersion()
        {
            try
            {
                Assembly assembly = typeof(SimpleTcpInstrumentation).Assembly;
                AssemblyInformationalVersionAttribute informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (informational != null && !String.IsNullOrEmpty(informational.InformationalVersion))
                {
                    string version = informational.InformationalVersion;
                    int plus = version.IndexOf('+');
                    return plus > 0 ? version.Substring(0, plus) : version;
                }

                Version assemblyVersion = assembly.GetName().Version;
                return assemblyVersion != null ? assemblyVersion.ToString() : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
