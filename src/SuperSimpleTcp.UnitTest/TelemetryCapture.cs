namespace SuperSimpleTcp.UnitTest
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// In-memory collector for the SuperSimpleTcp meter and activity source, scoped to one telemetry instance name
    /// so that tests do not observe each other's measurements.
    /// </summary>
    public sealed class TelemetryCapture : IDisposable
    {
        /// <summary>
        /// The instance name this capture filters on.
        /// </summary>
        public string InstanceName { get; }

        /// <summary>
        /// Source for test-owned ambient spans.
        /// </summary>
        public static readonly ActivitySource TestSource = new ActivitySource("SuperSimpleTcp.UnitTest");

        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;
        private readonly ConcurrentQueue<CapturedMeasurement> _Measurements = new ConcurrentQueue<CapturedMeasurement>();
        private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();
        private readonly bool _ThrowFromCallbacks;

        /// <summary>
        /// Start capturing.
        /// </summary>
        /// <param name="throwFromCallbacks">When true, every listener callback throws after recording, to prove instrumentation is best-effort.</param>
        public TelemetryCapture(bool throwFromCallbacks = false)
        {
            InstanceName = "test-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            _ThrowFromCallbacks = throwFromCallbacks;

            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimpleTcpTelemetryNames.MeterName) listener.EnableMeasurementEvents(instrument);
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == SimpleTcpTelemetryNames.ActivitySourceName || source.Name == "SuperSimpleTcp.UnitTest",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.Source.Name == SimpleTcpTelemetryNames.ActivitySourceName
                        && (activity.GetTagItem(SimpleTcpTelemetryNames.AttributeInstance) as string) == InstanceName)
                    {
                        _Activities.Enqueue(activity);
                        if (_ThrowFromCallbacks) throw new InvalidOperationException("listener failure (ActivityStopped)");
                    }
                },
                ActivityStarted = activity =>
                {
                    if (_ThrowFromCallbacks
                        && activity.Source.Name == SimpleTcpTelemetryNames.ActivitySourceName
                        && activity.OperationName == SimpleTcpTelemetryNames.SpanSend)
                    {
                        throw new InvalidOperationException("listener failure (ActivityStarted)");
                    }
                }
            };
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        /// <summary>
        /// Apply this capture's instance name to a server.
        /// </summary>
        public SimpleTcpServer Configure(SimpleTcpServer server)
        {
            server.Settings.Telemetry.InstanceName = InstanceName;
            return server;
        }

        /// <summary>
        /// Apply this capture's instance name to a client.
        /// </summary>
        public SimpleTcpClient Configure(SimpleTcpClient client)
        {
            client.Settings.Telemetry.InstanceName = InstanceName;
            return client;
        }

        /// <summary>
        /// All measurements for an instrument, optionally filtered by role.
        /// </summary>
        public List<CapturedMeasurement> Measurements(string instrument, string? role = null)
        {
            return _Measurements
                .Where(m => m.Instrument == instrument && (role == null || m.Tag(SimpleTcpTelemetryNames.AttributeRole) == role))
                .ToList();
        }

        /// <summary>
        /// Sum of measurement values for an instrument, optionally filtered by role and one additional tag.
        /// </summary>
        public double Sum(string instrument, string? role = null, string? tagKey = null, string? tagValue = null)
        {
            return Measurements(instrument, role)
                .Where(m => tagKey == null || m.Tag(tagKey) == tagValue)
                .Sum(m => m.Value);
        }

        /// <summary>
        /// Count of measurements for an instrument, optionally filtered by role and one additional tag.
        /// </summary>
        public int Count(string instrument, string? role = null, string? tagKey = null, string? tagValue = null)
        {
            return Measurements(instrument, role).Count(m => tagKey == null || m.Tag(tagKey) == tagValue);
        }

        /// <summary>
        /// Completed spans with the given name, optionally filtered by role.
        /// </summary>
        public List<Activity> Spans(string name, string? role = null)
        {
            return _Activities
                .Where(a => a.OperationName == name && (role == null || (a.GetTagItem(SimpleTcpTelemetryNames.AttributeRole) as string) == role))
                .ToList();
        }

        /// <summary>
        /// Trigger observable instrument callbacks (for example the build info gauge).
        /// </summary>
        public void RecordObservableInstruments()
        {
            _MeterListener.RecordObservableInstruments();
        }

        /// <summary>
        /// Poll until the condition holds or the timeout elapses.
        /// </summary>
        public static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 5000)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                await Task.Delay(25);
            }

            return condition();
        }

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Dispose()
        {
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
        }

        private void OnMeasurement<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags) where T : struct
        {
            Dictionary<string, string?> map = new Dictionary<string, string?>();
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                map[tag.Key] = tag.Value?.ToString();
            }

            bool mine = instrument.Name == SimpleTcpTelemetryNames.BuildInfo
                || (map.TryGetValue(SimpleTcpTelemetryNames.AttributeInstance, out string? instance) && instance == InstanceName);
            if (!mine) return;

            _Measurements.Enqueue(new CapturedMeasurement(instrument.Name, instrument.Unit, Convert.ToDouble(value), map));
            if (_ThrowFromCallbacks) throw new InvalidOperationException("listener failure (measurement)");
        }
    }
}
