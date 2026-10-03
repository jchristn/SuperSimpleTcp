namespace SuperSimpleTcp
{
    using System;

    /// <summary>
    /// Telemetry settings for a SimpleTcpServer or SimpleTcpClient.
    /// SuperSimpleTcp emits metrics and traces through the .NET base class library (a Meter and an ActivitySource, both named SuperSimpleTcp).
    /// It takes no dependency on any exporter; the host application subscribes a collector (for example Radiant or the OpenTelemetry SDK) to those names.
    /// Emission is enabled by default and costs a few nanoseconds per operation when nothing is listening.
    /// Instances are not thread-safe for concurrent writes; configure them before calling Start() or Connect().
    /// </summary>
    public class SimpleTcpTelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for this instance's telemetry.  Default true.
        /// When false, this instance emits no metrics and no spans regardless of EnableMetrics and EnableTraces.
        /// </summary>
        public bool Enable
        {
            get
            {
                return _Enable;
            }
            set
            {
                _Enable = value;
            }
        }

        /// <summary>
        /// Emit metrics on the SuperSimpleTcp meter.  Default true.  Has no effect when Enable is false.
        /// </summary>
        public bool EnableMetrics
        {
            get
            {
                return _EnableMetrics;
            }
            set
            {
                _EnableMetrics = value;
            }
        }

        /// <summary>
        /// Emit spans on the SuperSimpleTcp activity source.  Default true.  Has no effect when Enable is false.
        /// Spans are created only when a listener samples them, so leaving this on is free when no tracer is subscribed.
        /// </summary>
        public bool EnableTraces
        {
            get
            {
                return _EnableTraces;
            }
            set
            {
                _EnableTraces = value;
            }
        }

        /// <summary>
        /// Name used for the supersimpletcp.instance label on every metric and span, so that several servers or clients in one process can be told apart.
        /// Default "default".  Must be non-empty and at most 64 characters.
        /// Use a small, fixed set of names (for example "ingest" or "control"); never use per-connection or per-request values, because every distinct value creates new time series.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is longer than 64 characters.</exception>
        public string InstanceName
        {
            get
            {
                return _InstanceName;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(InstanceName), "InstanceName must be a non-empty string.");
                if (value.Length > 64) throw new ArgumentOutOfRangeException(nameof(InstanceName), "InstanceName must be 64 characters or fewer.");
                _InstanceName = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Enable = true;
        private bool _EnableMetrics = true;
        private bool _EnableTraces = true;
        private string _InstanceName = "default";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object with defaults: telemetry, metrics, and traces enabled, instance name "default".
        /// </summary>
        public SimpleTcpTelemetrySettings()
        {

        }

        #endregion
    }
}
