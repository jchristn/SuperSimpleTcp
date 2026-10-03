namespace SuperSimpleTcp
{
    using System;
    using System.Diagnostics;

    // Records metrics and spans for one SimpleTcpServer or SimpleTcpClient.
    // Every method is best-effort: a listener that throws, or any other telemetry failure, is swallowed so it can never affect networking.
    internal sealed class InstanceTelemetry
    {
        #region Private-Members

        private static readonly double _TicksPerSecond = Stopwatch.Frequency;
        private static readonly SimpleTcpTelemetrySettings _Defaults = new SimpleTcpTelemetrySettings();

        private readonly string _Role;
        private readonly bool _Tls;
        private readonly Func<SimpleTcpTelemetrySettings> _SettingsProvider;

        #endregion

        #region Constructors-and-Factories

        internal InstanceTelemetry(string role, bool tls, Func<SimpleTcpTelemetrySettings> settingsProvider)
        {
            if (String.IsNullOrEmpty(role)) throw new ArgumentNullException(nameof(role));
            if (settingsProvider == null) throw new ArgumentNullException(nameof(settingsProvider));

            _Role = role;
            _Tls = tls;
            _SettingsProvider = settingsProvider;
        }

        #endregion

        #region Internal-Methods

        internal static long Timestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        internal static double ElapsedSeconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) / _TicksPerSecond;
        }

        internal static string ErrorType(Exception e)
        {
            if (e == null) return null;
            return e.GetType().FullName;
        }

        internal static string ReasonValue(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.Kicked: return "kicked";
                case DisconnectReason.Timeout: return "timeout";
                default: return "normal";
            }
        }

        internal bool MetricsEnabled
        {
            get
            {
                SimpleTcpTelemetrySettings settings = GetSettings();
                return settings.Enable && settings.EnableMetrics;
            }
        }

        internal bool TracesEnabled
        {
            get
            {
                SimpleTcpTelemetrySettings settings = GetSettings();
                return settings.Enable && settings.EnableTraces && SimpleTcpInstrumentation.ActivitySource.HasListeners();
            }
        }

        #region Spans

        internal Activity StartActivity(string name, ActivityKind kind)
        {
            return StartActivity(name, kind, default(ActivityContext));
        }

        internal Activity StartActivity(string name, ActivityKind kind, ActivityContext parent)
        {
            if (!TracesEnabled) return null;

            try
            {
                Activity activity = parent != default(ActivityContext)
                    ? SimpleTcpInstrumentation.ActivitySource.StartActivity(name, kind, parent)
                    : SimpleTcpInstrumentation.ActivitySource.StartActivity(name, kind);

                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeRole, _Role);
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeInstance, GetSettings().InstanceName);
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeTls, _Tls);
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeNetworkTransport, "tcp");
                }

                return activity;
            }
            catch
            {
                return null;
            }
        }

        internal static void DetachAmbientSpan()
        {
            // Called at the top of background loops so that per-connection and per-segment spans start new
            // traces instead of nesting under whatever span was ambient when Start() or Connect() was called.
            try
            {
                Activity.Current = null;
            }
            catch
            {
            }
        }

        internal static void SetPeer(Activity activity, string ipPort)
        {
            if (activity == null || !activity.IsAllDataRequested || String.IsNullOrEmpty(ipPort)) return;

            try
            {
                int colon = ipPort.LastIndexOf(':');
                if (colon > 0)
                {
                    string ip = ipPort.Substring(0, colon).Trim('[', ']');
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeNetworkPeerAddress, ip);
                    if (Int32.TryParse(ipPort.Substring(colon + 1), out int port))
                    {
                        activity.SetTag(SimpleTcpTelemetryNames.AttributeNetworkPeerPort, port);
                    }
                }
                else
                {
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeNetworkPeerAddress, ipPort);
                }
            }
            catch
            {
            }
        }

        internal static void SetTag(Activity activity, string key, object value)
        {
            if (activity == null || !activity.IsAllDataRequested) return;

            try
            {
                activity.SetTag(key, value);
            }
            catch
            {
            }
        }

        internal static void SetOutcome(Activity activity, string outcome)
        {
            if (activity == null) return;

            try
            {
                if (activity.IsAllDataRequested) activity.SetTag(SimpleTcpTelemetryNames.AttributeOutcome, outcome);
                if (outcome == SimpleTcpTelemetryNames.OutcomeSuccess) activity.SetStatus(ActivityStatusCode.Ok);
            }
            catch
            {
            }
        }

        internal static void SetFailure(Activity activity, string outcome, string description)
        {
            if (activity == null) return;

            try
            {
                if (activity.IsAllDataRequested) activity.SetTag(SimpleTcpTelemetryNames.AttributeOutcome, outcome);
                activity.SetStatus(ActivityStatusCode.Error, description);
            }
            catch
            {
            }
        }

        internal static void SetException(Activity activity, string outcome, Exception e)
        {
            if (activity == null || e == null) return;

            try
            {
                activity.SetStatus(ActivityStatusCode.Error, e.Message);

                if (activity.IsAllDataRequested)
                {
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeOutcome, outcome);
                    activity.SetTag(SimpleTcpTelemetryNames.AttributeErrorType, ErrorType(e));

                    ActivityTagsCollection tags = new ActivityTagsCollection
                    {
                        { "exception.type", ErrorType(e) },
                        { "exception.message", e.Message },
                        { "exception.stacktrace", e.ToString() }
                    };

                    activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
                }
            }
            catch
            {
            }
        }

        internal static void Stop(Activity activity)
        {
            if (activity == null) return;

            try
            {
                activity.Dispose();
            }
            catch
            {
            }
        }

        internal static string OutcomeFor(Exception e)
        {
            if (e is TimeoutException) return SimpleTcpTelemetryNames.OutcomeTimeout;
            if (e is OperationCanceledException) return SimpleTcpTelemetryNames.OutcomeCanceled;
            return SimpleTcpTelemetryNames.OutcomeError;
        }

        internal static string TlsVersion(System.Security.Authentication.SslProtocols protocol)
        {
            switch ((int)protocol)
            {
                case 192: return "1.0";
                case 768: return "1.1";
                case 3072: return "1.2";
                case 12288: return "1.3";
                default: return protocol.ToString();
            }
        }

        internal void EndSend(Activity activity, long startTimestamp, string outcome, Exception e, long bytes)
        {
            double seconds = ElapsedSeconds(startTimestamp);
            if (bytes > 0) SetTag(activity, SimpleTcpTelemetryNames.AttributeBytes, bytes);
            Finish(activity, outcome, e);
            SendCompleted(outcome, seconds, e);
            Stop(activity);
        }

        internal void EndConnect(Activity activity, long startTimestamp, string outcome, Exception e)
        {
            double seconds = ElapsedSeconds(startTimestamp);
            Finish(activity, outcome, e);
            ConnectCompleted(outcome, seconds, e);
            Stop(activity);
        }

        internal void EndTlsHandshake(Activity activity, long startTimestamp, string outcome, Exception e)
        {
            double seconds = ElapsedSeconds(startTimestamp);
            Finish(activity, outcome, e);
            TlsHandshakeCompleted(outcome, seconds, e);
            Stop(activity);
        }

        internal static void Finish(Activity activity, string outcome, Exception e)
        {
            if (activity == null) return;
            if (e != null) SetException(activity, outcome, e);
            else if (outcome == SimpleTcpTelemetryNames.OutcomeSuccess || outcome == SimpleTcpTelemetryNames.OutcomeCanceled) SetOutcome(activity, outcome);
            else SetFailure(activity, outcome, outcome);
        }

        #endregion

        #region Metrics

        internal void ListenerStarted(int connectionLimit)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                SimpleTcpInstrumentation.ListenersActive.Add(1, tags);
                SimpleTcpInstrumentation.ConnectionsLimit.Add(connectionLimit, tags);
            }
            catch
            {
            }
        }

        internal void ListenerStopped(int connectionLimit)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                SimpleTcpInstrumentation.ListenersActive.Add(-1, tags);
                SimpleTcpInstrumentation.ConnectionsLimit.Add(-connectionLimit, tags);
            }
            catch
            {
            }
        }

        internal void ConnectionOpened()
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                SimpleTcpInstrumentation.ConnectionsOpened.Add(1, tags);
                SimpleTcpInstrumentation.ConnectionsActive.Add(1, tags);
            }
            catch
            {
            }
        }

        internal void ConnectionClosed(DisconnectReason reason, double durationSeconds)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                SimpleTcpInstrumentation.ConnectionsActive.Add(-1, tags);

                tags.Add(SimpleTcpTelemetryNames.AttributeReason, ReasonValue(reason));
                SimpleTcpInstrumentation.ConnectionsClosed.Add(1, tags);
                SimpleTcpInstrumentation.ConnectionDuration.Record(durationSeconds, tags);
            }
            catch
            {
            }
        }

        internal void ConnectionRejected(string rejectReason)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                tags.Add(SimpleTcpTelemetryNames.AttributeRejectReason, rejectReason);
                SimpleTcpInstrumentation.ConnectionsRejected.Add(1, tags);
            }
            catch
            {
            }
        }

        internal void AcceptCompleted(string outcome, double seconds)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                tags.Add(SimpleTcpTelemetryNames.AttributeOutcome, outcome);
                SimpleTcpInstrumentation.AcceptDuration.Record(seconds, tags);
            }
            catch
            {
            }
        }

        internal void TlsHandshakeCompleted(string outcome, double seconds, Exception e)
        {
            if (!MetricsEnabled) return;

            try
            {
                SimpleTcpInstrumentation.TlsHandshakeDuration.Record(seconds, OutcomeTags(outcome, e));
            }
            catch
            {
            }

            if (e != null) Error(SimpleTcpTelemetryNames.OperationTlsHandshake, e);
        }

        internal void ConnectCompleted(string outcome, double seconds, Exception e)
        {
            if (!MetricsEnabled) return;

            try
            {
                SimpleTcpInstrumentation.ConnectDuration.Record(seconds, OutcomeTags(outcome, e));
            }
            catch
            {
            }

            if (e != null) Error(SimpleTcpTelemetryNames.OperationConnect, e);
        }

        internal void SendCompleted(string outcome, double seconds, Exception e)
        {
            if (!MetricsEnabled) return;

            try
            {
                SimpleTcpInstrumentation.SendDuration.Record(seconds, OutcomeTags(outcome, e));
            }
            catch
            {
            }

            if (e != null) Error(SimpleTcpTelemetryNames.OperationSend, e);
        }

        internal void SendLockWaited(double seconds)
        {
            if (!MetricsEnabled) return;

            try
            {
                SimpleTcpInstrumentation.SendLockWaitDuration.Record(seconds, BaseTags());
            }
            catch
            {
            }
        }

        internal void BytesSent(long bytes)
        {
            if (bytes <= 0 || !MetricsEnabled) return;

            try
            {
                SimpleTcpInstrumentation.SentBytes.Add(bytes, BaseTags());
            }
            catch
            {
            }
        }

        internal void SegmentReceived(int bytes)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                SimpleTcpInstrumentation.ReceivedBytes.Add(bytes, tags);
                SimpleTcpInstrumentation.ReceiveSegmentSize.Record(bytes, tags);
            }
            catch
            {
            }
        }

        internal void HandlerCompleted(string dispatchMode, string outcome, double seconds, Exception e)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = OutcomeTags(outcome, e);
                tags.Add(SimpleTcpTelemetryNames.AttributeDispatchMode, dispatchMode);
                SimpleTcpInstrumentation.HandlerDuration.Record(seconds, tags);
            }
            catch
            {
            }

            if (e != null) Error(SimpleTcpTelemetryNames.OperationHandler, e);
        }

        internal void DispatchEnqueued()
        {
            if (!MetricsEnabled) return;

            try
            {
                SimpleTcpInstrumentation.DispatchQueueDepth.Add(1, BaseTags());
            }
            catch
            {
            }
        }

        internal void DispatchDequeued(double waitSeconds)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                SimpleTcpInstrumentation.DispatchQueueDepth.Add(-1, tags);
                SimpleTcpInstrumentation.DispatchQueueWait.Record(waitSeconds, tags);
            }
            catch
            {
            }
        }

        internal void DispatchDropped(int count)
        {
            if (count <= 0 || !MetricsEnabled) return;

            try
            {
                SimpleTcpInstrumentation.DispatchQueueDepth.Add(-count, BaseTags());
            }
            catch
            {
            }
        }

        internal void MonitorRun(string monitor, Exception e)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                tags.Add(SimpleTcpTelemetryNames.AttributeMonitor, monitor);
                tags.Add(SimpleTcpTelemetryNames.AttributeOutcome, e == null ? SimpleTcpTelemetryNames.OutcomeSuccess : SimpleTcpTelemetryNames.OutcomeError);
                SimpleTcpInstrumentation.MonitorRuns.Add(1, tags);
            }
            catch
            {
            }

            if (e != null) Error(SimpleTcpTelemetryNames.OperationMonitor, e);
        }

        internal void Error(string operation, Exception e)
        {
            if (!MetricsEnabled) return;

            try
            {
                TagList tags = BaseTags();
                tags.Add(SimpleTcpTelemetryNames.AttributeOperation, operation);
                tags.Add(SimpleTcpTelemetryNames.AttributeErrorType, e != null ? ErrorType(e) : "unknown");
                SimpleTcpInstrumentation.Errors.Add(1, tags);
            }
            catch
            {
            }
        }

        #endregion

        #endregion

        #region Private-Methods

        private SimpleTcpTelemetrySettings GetSettings()
        {
            SimpleTcpTelemetrySettings settings = null;

            try
            {
                settings = _SettingsProvider();
            }
            catch
            {
            }

            return settings ?? _Defaults;
        }

        private TagList BaseTags()
        {
            TagList tags = new TagList();
            tags.Add(SimpleTcpTelemetryNames.AttributeRole, _Role);
            tags.Add(SimpleTcpTelemetryNames.AttributeInstance, GetSettings().InstanceName);
            tags.Add(SimpleTcpTelemetryNames.AttributeTls, _Tls ? "true" : "false");
            return tags;
        }

        private TagList OutcomeTags(string outcome, Exception e)
        {
            TagList tags = BaseTags();
            tags.Add(SimpleTcpTelemetryNames.AttributeOutcome, outcome);
            if (e != null) tags.Add(SimpleTcpTelemetryNames.AttributeErrorType, ErrorType(e));
            return tags;
        }

        #endregion
    }
}
