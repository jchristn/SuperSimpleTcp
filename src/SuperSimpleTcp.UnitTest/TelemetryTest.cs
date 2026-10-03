namespace SuperSimpleTcp.UnitTest
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using N = SuperSimpleTcp.SimpleTcpTelemetryNames;

    [TestClass]
    [DoNotParallelize]
    public class TelemetryTest
    {
        private const string LoopbackIp = "127.0.0.1";
        private const string Server = N.RoleServer;
        private const string Client = N.RoleClient;

        private static string CertificatePath => Path.Combine(AppContext.BaseDirectory, "simpletcp.pfx");

        #region Contract

        [TestMethod]
        public void Names_AreStablePublicContract()
        {
            Dictionary<string, string> expected = new Dictionary<string, string>
            {
                { nameof(N.MeterName), "SuperSimpleTcp" },
                { nameof(N.ActivitySourceName), "SuperSimpleTcp" },
                { nameof(N.ConnectionsActive), "supersimpletcp.connections.active" },
                { nameof(N.SendDuration), "supersimpletcp.send.duration" },
                { nameof(N.SpanAccept), "supersimpletcp accept" },
                { nameof(N.AttributeErrorType), "error.type" }
            };

            foreach (KeyValuePair<string, string> pair in expected)
            {
                object? actual = typeof(SimpleTcpTelemetryNames).GetField(pair.Key)?.GetValue(null);
                Assert.AreEqual(pair.Value, actual, pair.Key);
            }

            // Application metric families are prefixed with the product name.
            List<string> values = typeof(SimpleTcpTelemetryNames).GetFields()
                .Where(f => f.IsLiteral)
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();
            Assert.IsTrue(values.Where(v => v.StartsWith("supersimpletcp.")).Count() >= 20);
        }

        [TestMethod]
        public void TelemetrySettings_DefaultsAndValidation()
        {
            SimpleTcpTelemetrySettings settings = new SimpleTcpTelemetrySettings();
            Assert.IsTrue(settings.Enable);
            Assert.IsTrue(settings.EnableMetrics);
            Assert.IsTrue(settings.EnableTraces);
            Assert.AreEqual("default", settings.InstanceName);

            Assert.ThrowsExactly<ArgumentNullException>(() => settings.InstanceName = "");
            Assert.ThrowsExactly<ArgumentNullException>(() => settings.InstanceName = null!);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.InstanceName = new string('x', 65));
            settings.InstanceName = new string('x', 64);

            SimpleTcpServerSettings serverSettings = new SimpleTcpServerSettings();
            serverSettings.Telemetry = null!;
            Assert.IsNotNull(serverSettings.Telemetry);

            SimpleTcpClientSettings clientSettings = new SimpleTcpClientSettings();
            clientSettings.Telemetry = null!;
            Assert.IsNotNull(clientSettings.Telemetry);
        }

        [TestMethod]
        public void BuildInfo_ReportsVersion()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using (SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0)))
            {
                // The meter is created when the library first records; use it once so the gauge exists.
                server.Start();
                server.Stop();
            }

            capture.RecordObservableInstruments();

            CapturedMeasurement? info = capture.Measurements(N.BuildInfo).LastOrDefault();
            Assert.IsNotNull(info, "build info gauge should be observable");
            Assert.AreEqual(1, info.Value);
            Assert.IsFalse(string.IsNullOrEmpty(info.Tag(N.AttributeVersion)));
        }

        #endregion

        #region Lifecycle

        [TestMethod]
        public async Task Lifecycle_AsyncDispatch_EmitsConnectionSendReceiveHandlerAndQueueMetrics()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            TaskCompletionSource<bool> serverReceived = NewSignal();
            TaskCompletionSource<bool> clientReceived = NewSignal();

            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Events.DataReceived += (sender, e) =>
            {
                ((SimpleTcpServer)sender!).Send(e.IpPort, "pong");
                serverReceived.TrySetResult(true);
            };
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
            client.Events.DataReceived += (_, _) => clientReceived.TrySetResult(true);
            client.Connect();
            await client.SendAsync("ping");

            await WithTimeout(serverReceived.Task);
            await WithTimeout(clientReceived.Task);

            client.Disconnect();
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.ConnectionsClosed, Server) == 1), "server should record the close");
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.ConnectionsClosed, Client) == 1), "client should record the close");
            server.Stop();
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Sum(N.ListenersActive, Server) == 0 && capture.Count(N.ListenersActive, Server) == 2));

            // Connections: opened once per side, active returns to zero, close carries a reason and a duration.
            Assert.AreEqual(1, capture.Sum(N.ConnectionsOpened, Server));
            Assert.AreEqual(1, capture.Sum(N.ConnectionsOpened, Client));
            Assert.AreEqual(0, capture.Sum(N.ConnectionsActive, Server));
            Assert.AreEqual(0, capture.Sum(N.ConnectionsActive, Client));
            Assert.AreEqual(2, capture.Count(N.ConnectionsActive, Server));
            Assert.AreEqual(1, capture.Count(N.ConnectionDuration, Server, N.AttributeReason, "normal"));
            Assert.AreEqual(1, capture.Count(N.ConnectionDuration, Client, N.AttributeReason, "normal"));

            // Capacity follows MaxConnections while listening.
            CapturedMeasurement limit = capture.Measurements(N.ConnectionsLimit, Server).First();
            Assert.AreEqual(server.Settings.MaxConnections, limit.Value);
            Assert.AreEqual(0, capture.Sum(N.ConnectionsLimit, Server));

            // Accept and connect durations with outcome.
            Assert.AreEqual(1, capture.Count(N.AcceptDuration, Server, N.AttributeOutcome, N.OutcomeSuccess));
            Assert.AreEqual(1, capture.Count(N.ConnectDuration, Client, N.AttributeOutcome, N.OutcomeSuccess));

            // Bytes, segments, send duration, and lock wait.
            Assert.AreEqual(4, capture.Sum(N.SentBytes, Client));
            Assert.AreEqual(4, capture.Sum(N.SentBytes, Server));
            Assert.AreEqual(4, capture.Sum(N.ReceivedBytes, Server));
            Assert.AreEqual(4, capture.Sum(N.ReceivedBytes, Client));
            Assert.AreEqual(1, capture.Count(N.ReceiveSegmentSize, Server));
            Assert.AreEqual(1, capture.Count(N.SendDuration, Client, N.AttributeOutcome, N.OutcomeSuccess));
            Assert.AreEqual(1, capture.Count(N.SendDuration, Server, N.AttributeOutcome, N.OutcomeSuccess));
            Assert.AreEqual(1, capture.Count(N.SendLockWaitDuration, Client));
            Assert.AreEqual(1, capture.Count(N.SendLockWaitDuration, Server));

            // Async DataReceived dispatch: handler timing, queue wait, and a queue that drains back to zero.
            Assert.AreEqual(1, capture.Count(N.HandlerDuration, Server, N.AttributeDispatchMode, N.DispatchAsync));
            Assert.AreEqual(1, capture.Count(N.DispatchQueueWait, Server));
            Assert.AreEqual(0, capture.Sum(N.DispatchQueueDepth, Server));
            Assert.AreEqual(2, capture.Count(N.DispatchQueueDepth, Server));

            // Units are UCUM.
            Assert.AreEqual("s", capture.Measurements(N.SendDuration).First().Unit);
            Assert.AreEqual("By", capture.Measurements(N.SentBytes).First().Unit);

            // Every metric carries the bounded base labels and never a peer address.
            foreach (CapturedMeasurement m in capture.Measurements(N.SendDuration).Concat(capture.Measurements(N.ConnectionsOpened)))
            {
                Assert.AreEqual(capture.InstanceName, m.Tag(N.AttributeInstance));
                Assert.AreEqual("false", m.Tag(N.AttributeTls));
                Assert.IsNull(m.Tag(N.AttributeNetworkPeerAddress));
                Assert.IsNull(m.Tag(N.AttributeNetworkPeerPort));
            }

            Assert.AreEqual(0, capture.Sum(N.Errors), "a clean exchange should record no errors: " + string.Join("; ", capture.Measurements(N.Errors).Select(m => m.Tag(N.AttributeRole) + "/" + m.Tag(N.AttributeOperation) + "/" + m.Tag(N.AttributeErrorType))));
        }

        [TestMethod]
        public async Task Spans_AsyncDispatch_ProcessIsChildOfReceiveAcrossHandOff_AndBackgroundSpansAreRoots()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            TaskCompletionSource<bool> serverReceived = NewSignal();
            string? handlerParentSpanName = null;

            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Events.DataReceived += (_, _) =>
            {
                handlerParentSpanName = Activity.Current?.OperationName;
                serverReceived.TrySetResult(true);
            };

            ActivityTraceId outerTraceId;
            using (Activity? outer = TelemetryCapture.TestSource.StartActivity("host startup"))
            {
                Assert.IsNotNull(outer);
                outerTraceId = outer.TraceId;
                server.Start();

                using SimpleTcpClient c = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
                c.Connect();
                c.Send("hello");
                await WithTimeout(serverReceived.Task);
                c.Disconnect();
            }

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Spans(N.SpanDisconnect, Server).Count == 1));
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Spans(N.SpanProcess, Server).Count == 1));

            // The connect span follows the caller's context.
            Activity connect = capture.Spans(N.SpanConnect, Client).Single();
            Assert.AreEqual(outerTraceId, connect.TraceId, "connect should join the caller's trace");
            Assert.AreEqual(ActivityKind.Client, connect.Kind);
            Assert.AreEqual(ActivityStatusCode.Ok, connect.Status);
            Assert.AreEqual(LoopbackIp, connect.GetTagItem(N.AttributeServerAddress));

            // The accept span is a root even though Start() ran under an ambient span.
            Activity accept = capture.Spans(N.SpanAccept, Server).Single();
            Assert.AreEqual(ActivityKind.Server, accept.Kind);
            Assert.AreEqual(default(ActivitySpanId), accept.ParentSpanId, "accept should start a new trace");
            Assert.AreEqual(ActivityStatusCode.Ok, accept.Status);
            Assert.AreEqual(LoopbackIp, accept.GetTagItem(N.AttributeNetworkPeerAddress));
            Assert.AreEqual("tcp", accept.GetTagItem(N.AttributeNetworkTransport));

            // receive -> process across the asynchronous hand-off; the handler sees the process span as current.
            Activity receive = capture.Spans(N.SpanReceive, Server).Single();
            Activity process = capture.Spans(N.SpanProcess, Server).Single();
            Assert.AreEqual(ActivityKind.Consumer, receive.Kind);
            Assert.AreEqual(default(ActivitySpanId), receive.ParentSpanId, "receive should not nest under the accept or host span");
            Assert.AreEqual(receive.TraceId, process.TraceId);
            Assert.AreEqual(receive.SpanId, process.ParentSpanId);
            Assert.AreEqual(N.DispatchAsync, process.GetTagItem(N.AttributeDispatchMode));
            Assert.AreEqual(5, receive.GetTagItem(N.AttributeBytes));
            Assert.AreEqual(N.SpanProcess, handlerParentSpanName, "application spans inside DataReceived should nest under process");

            // send and disconnect.
            Activity send = capture.Spans(N.SpanSend, Client).Single();
            Assert.AreEqual(ActivityKind.Client, send.Kind);
            Assert.AreEqual(5L, send.GetTagItem(N.AttributeBytes));
            Activity disconnect = capture.Spans(N.SpanDisconnect, Server).Single();
            Assert.AreEqual("normal", disconnect.GetTagItem(N.AttributeReason));
            Assert.IsNotNull(disconnect.GetTagItem(N.AttributeConnectionDuration));

            server.Stop();
        }

        [TestMethod]
        public async Task SyncDispatch_RecordsHandlerInlineAndNoQueueMetrics()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            TaskCompletionSource<bool> serverReceived = NewSignal();

            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Settings.UseAsyncDataReceivedEvents = false;
            server.Events.DataReceived += (_, _) => serverReceived.TrySetResult(true);
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
            client.Connect();
            client.Send("sync");
            await WithTimeout(serverReceived.Task);
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.HandlerDuration, Server) == 1));

            Assert.AreEqual(1, capture.Count(N.HandlerDuration, Server, N.AttributeDispatchMode, N.DispatchSync));
            Assert.AreEqual(0, capture.Count(N.DispatchQueueDepth, Server));

            Activity receive = capture.Spans(N.SpanReceive, Server).Single();
            Activity process = capture.Spans(N.SpanProcess, Server).Single();
            Assert.AreEqual(receive.SpanId, process.ParentSpanId);

            client.Disconnect();
            server.Stop();
        }

        #endregion

        #region Rejections-and-Disconnects

        [TestMethod]
        public async Task Accept_NotPermitted_RecordsRejection()
        {
            await AssertRejected(server => server.Settings.PermittedIPs.Add("10.255.255.1"), N.RejectNotPermitted, ActivityStatusCode.Unset);
        }

        [TestMethod]
        public async Task Accept_Blocked_RecordsRejection()
        {
            await AssertRejected(server => server.Settings.BlockedIPs.Add(LoopbackIp), N.RejectBlocked, ActivityStatusCode.Unset);
        }

        [TestMethod]
        public async Task Accept_MaxConnections_RecordsRejectionAsError()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Settings.MaxConnections = 1;
            server.Start();

            using SimpleTcpClient first = new SimpleTcpClient(LoopbackIp, server.Port);
            first.Connect();
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => server.Connections == 1));

            using TcpClient second = new TcpClient();
            await second.ConnectAsync(LoopbackIp, server.Port);

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.ConnectionsRejected, Server, N.AttributeRejectReason, N.RejectMaxConnections) == 1));
            Assert.AreEqual(1, capture.Count(N.AcceptDuration, Server, N.AttributeOutcome, N.OutcomeRejected));

            Activity rejected = capture.Spans(N.SpanAccept, Server).Single(a => (a.GetTagItem(N.AttributeRejectReason) as string) == N.RejectMaxConnections);
            Assert.AreEqual(ActivityStatusCode.Error, rejected.Status, "capacity rejection should be visible in error filters");

            first.Disconnect();
            server.Stop();
        }

        [TestMethod]
        public async Task DisconnectClient_RecordsKickedReason()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Start();

            using SimpleTcpClient client = new SimpleTcpClient(LoopbackIp, server.Port);
            client.Connect();
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => server.Connections == 1));

            server.DisconnectClient(server.GetClients().Single());

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.ConnectionsClosed, Server, N.AttributeReason, "kicked") == 1));
            Assert.AreEqual("kicked", capture.Spans(N.SpanDisconnect, Server).Single().GetTagItem(N.AttributeReason));
            server.Stop();
        }

        [TestMethod]
        public async Task IdleClientMonitor_RecordsTimeoutCloseAndMonitorRuns()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Settings.IdleClientTimeoutMs = 300;
            server.Settings.IdleClientEvaluationIntervalMs = 100;
            server.Start();

            using SimpleTcpClient client = new SimpleTcpClient(LoopbackIp, server.Port);
            client.Connect();

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.ConnectionsClosed, Server, N.AttributeReason, "timeout") == 1));
            Assert.IsTrue(capture.Count(N.MonitorRuns, Server, N.AttributeMonitor, N.MonitorIdleClient) >= 1);
            Assert.AreEqual(0, capture.Count(N.MonitorRuns, Server, N.AttributeOutcome, N.OutcomeError));
            server.Stop();
        }

        [TestMethod]
        public async Task ClientMonitors_RecordRuns()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = new SimpleTcpServer(LoopbackIp, 0);
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
            client.Settings.IdleServerEvaluationIntervalMs = 50;
            client.Settings.ConnectionLostEvaluationIntervalMs = 50;
            client.Connect();

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() =>
                capture.Count(N.MonitorRuns, Client, N.AttributeMonitor, N.MonitorIdleServer) >= 1
                && capture.Count(N.MonitorRuns, Client, N.AttributeMonitor, N.MonitorConnectionLost) >= 1));

            client.Disconnect();
            server.Stop();
        }

        #endregion

        #region Failure-Paths

        [TestMethod]
        public void Connect_Refused_RecordsErrorOutcomeErrorCounterAndSpanException()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            int port = UnusedPort();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, port));
            client.Settings.ConnectTimeoutMs = 2000;
            Assert.ThrowsExactly<SocketException>(() => client.Connect());

            Assert.AreEqual(1, capture.Count(N.ConnectDuration, Client, N.AttributeOutcome, N.OutcomeError));
            Assert.AreEqual(1, capture.Count(N.Errors, Client, N.AttributeOperation, N.OperationConnect));
            Assert.AreEqual(typeof(SocketException).FullName, capture.Measurements(N.Errors, Client).Single().Tag(N.AttributeErrorType));

            Activity connect = capture.Spans(N.SpanConnect, Client).Single();
            Assert.AreEqual(ActivityStatusCode.Error, connect.Status);
            Assert.AreEqual(typeof(SocketException).FullName, connect.GetTagItem(N.AttributeErrorType));
            Assert.IsTrue(connect.Events.Any(e => e.Name == "exception"), "exception event should be recorded on the span");
            Assert.AreEqual(0, capture.Count(N.ConnectionsOpened, Client));
        }

        [TestMethod]
        public void ConnectWithRetries_Timeout_RecordsAttemptsUnderParentSpan()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            int port = UnusedPort();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, port));
            Assert.ThrowsExactly<TimeoutException>(() => client.ConnectWithRetries(700));

            Activity parent = capture.Spans(N.SpanConnectWithRetries, Client).Single();
            Assert.AreEqual(ActivityStatusCode.Error, parent.Status);
            Assert.AreEqual(N.OutcomeTimeout, parent.GetTagItem(N.AttributeOutcome));

            List<Activity> attempts = capture.Spans(N.SpanConnect, Client);
            Assert.IsTrue(attempts.Count >= 1);
            Assert.IsTrue(attempts.All(a => a.ParentSpanId == parent.SpanId), "each attempt should be a child of connect_with_retries");
            Assert.AreEqual(1, attempts[0].GetTagItem(N.AttributeAttempt));
            Assert.AreEqual(attempts.Count, capture.Count(N.ConnectDuration, Client));
        }

        [TestMethod]
        public async Task ServerSend_UnknownClient_RecordsNotFound()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Start();

            server.Send("127.0.0.1:1", "nobody home");
            await server.SendAsync("127.0.0.1:1", "nobody home");

            Assert.AreEqual(2, capture.Count(N.SendDuration, Server, N.AttributeOutcome, N.OutcomeNotFound));
            Assert.AreEqual(0, capture.Sum(N.SentBytes, Server));
            Assert.IsTrue(capture.Spans(N.SpanSend, Server).All(a => a.Status == ActivityStatusCode.Error));
            server.Stop();
        }

        [TestMethod]
        public async Task ClientSendAsync_Canceled_RecordsCanceledWithoutError()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = new SimpleTcpServer(LoopbackIp, 0);
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
            client.Connect();

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            await client.SendAsync("never sent", cts.Token);

            Assert.AreEqual(1, capture.Count(N.SendDuration, Client, N.AttributeOutcome, N.OutcomeCanceled));
            Assert.AreEqual(0, capture.Count(N.Errors, Client));
            Assert.AreNotEqual(ActivityStatusCode.Error, capture.Spans(N.SpanSend, Client).Single().Status);

            client.Disconnect();
            server.Stop();
        }

        [TestMethod]
        public async Task SyncHandlerException_RecordsHandlerErrorAndReceiveTermination()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Settings.UseAsyncDataReceivedEvents = false;
            server.Events.DataReceived += (_, _) => throw new InvalidDataException("bad frame");
            server.Start();

            using SimpleTcpClient client = new SimpleTcpClient(LoopbackIp, server.Port);
            client.Connect();
            client.Send("boom");

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.ConnectionsClosed, Server) == 1));

            Assert.AreEqual(1, capture.Count(N.HandlerDuration, Server, N.AttributeOutcome, N.OutcomeError));
            Assert.AreEqual(1, capture.Count(N.Errors, Server, N.AttributeOperation, N.OperationHandler));
            Assert.AreEqual(1, capture.Count(N.Errors, Server, N.AttributeOperation, N.OperationReceive));
            Assert.AreEqual(typeof(InvalidDataException).FullName,
                capture.Measurements(N.Errors, Server).First(m => m.Tag(N.AttributeOperation) == N.OperationHandler).Tag(N.AttributeErrorType));

            Activity process = capture.Spans(N.SpanProcess, Server).Single();
            Assert.AreEqual(ActivityStatusCode.Error, process.Status);
            Assert.AreEqual(ActivityStatusCode.Error, capture.Spans(N.SpanReceive, Server).Single().Status);
            Assert.AreEqual(ActivityStatusCode.Error, capture.Spans(N.SpanDisconnect, Server).Single().Status);
            server.Stop();
        }

        [TestMethod]
        public async Task AsyncHandlerException_RecordsHandlerErrorAndWorkerKeepsDelivering()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            int calls = 0;
            List<string> logs = new List<string>();

            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Logger = msg => { lock (logs) logs.Add(msg); };
            server.Events.DataReceived += (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1) throw new InvalidDataException("bad frame");
            };
            server.Start();

            using SimpleTcpClient client = new SimpleTcpClient(LoopbackIp, server.Port);
            client.Connect();
            client.Send("first");

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.HandlerDuration, Server, N.AttributeOutcome, N.OutcomeError) == 1));
            Assert.AreEqual(N.DispatchAsync, capture.Measurements(N.HandlerDuration, Server).Single().Tag(N.AttributeDispatchMode));
            Assert.AreEqual(1, capture.Count(N.Errors, Server, N.AttributeOperation, N.OperationHandler));
            Assert.AreEqual(ActivityStatusCode.Error, capture.Spans(N.SpanProcess, Server).Single().Status);

            // The dispatch worker survives a throwing handler: later segments are still delivered and the queue drains.
            await Task.Delay(100);
            client.Send("second");
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => Volatile.Read(ref calls) == 2));
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.HandlerDuration, Server, N.AttributeOutcome, N.OutcomeSuccess) == 1));
            Assert.AreEqual(0, capture.Sum(N.DispatchQueueDepth, Server));
            Assert.IsTrue(server.IsListening);
            Assert.AreEqual(1, server.GetClients().Count());
            lock (logs) Assert.IsTrue(logs.Any(l => l.Contains("DataReceived handler exception") && l.Contains("bad frame")));

            client.Disconnect();
            server.Stop();
        }

        [TestMethod]
        public async Task AsyncHandlerException_ClientWorkerKeepsDelivering()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            int calls = 0;

            using SimpleTcpServer server = new SimpleTcpServer(LoopbackIp, 0);
            string? clientIpPort = null;
            server.Events.ClientConnected += (_, e) => clientIpPort = e.IpPort;
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
            client.Events.DataReceived += (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1) throw new InvalidDataException("bad frame");
            };
            client.Connect();
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => clientIpPort != null));

            server.Send(clientIpPort!, "first");
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.HandlerDuration, Client, N.AttributeOutcome, N.OutcomeError) == 1));

            await Task.Delay(100);
            server.Send(clientIpPort!, "second");
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => Volatile.Read(ref calls) == 2));
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.HandlerDuration, Client, N.AttributeOutcome, N.OutcomeSuccess) == 1));
            Assert.AreEqual(0, capture.Sum(N.DispatchQueueDepth, Client));
            Assert.IsTrue(client.IsConnected);

            client.Disconnect();
            server.Stop();
        }

        #endregion

        #region Tls

        [TestMethod]
        public async Task Tls_Success_RecordsHandshakeOnBothSidesAsChildSpans()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer($"{LoopbackIp}:0", true, CertificatePath, "simpletcp"));
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient($"{LoopbackIp}:{server.Port}", true, CertificatePath, "simpletcp"));
            client.Settings.AcceptInvalidCertificates = true;
            client.Settings.MutuallyAuthenticate = false;
            client.Connect();

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.TlsHandshakeDuration, Server) == 1));
            Assert.AreEqual(1, capture.Count(N.TlsHandshakeDuration, Server, N.AttributeOutcome, N.OutcomeSuccess));
            Assert.AreEqual(1, capture.Count(N.TlsHandshakeDuration, Client, N.AttributeOutcome, N.OutcomeSuccess));
            Assert.AreEqual("true", capture.Measurements(N.ConnectionsOpened, Server).Single().Tag(N.AttributeTls));

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Spans(N.SpanAccept, Server).Count == 1));
            Activity accept = capture.Spans(N.SpanAccept, Server).Single();
            Activity serverTls = capture.Spans(N.SpanTlsHandshake, Server).Single();
            Assert.AreEqual(accept.SpanId, serverTls.ParentSpanId);
            Assert.AreEqual("1.2", serverTls.GetTagItem(N.AttributeTlsProtocolVersion));

            Activity connect = capture.Spans(N.SpanConnect, Client).Single();
            Activity clientTls = capture.Spans(N.SpanTlsHandshake, Client).Single();
            Assert.AreEqual(connect.SpanId, clientTls.ParentSpanId);

            client.Disconnect();
            server.Stop();
        }

        [TestMethod]
        public async Task Tls_HandshakeFailure_RecordsFailureAndRejection()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer($"{LoopbackIp}:0", true, CertificatePath, "simpletcp"));
            server.Settings.AcceptInvalidCertificates = true;
            server.Start();

            // A plaintext peer sends garbage instead of a TLS ClientHello.
            using (TcpClient plain = new TcpClient())
            {
                await plain.ConnectAsync(LoopbackIp, server.Port);
                byte[] garbage = Encoding.ASCII.GetBytes("this is not a tls client hello\r\n\r\n");
                await plain.GetStream().WriteAsync(garbage, 0, garbage.Length);

                Assert.IsTrue(await TelemetryCapture.WaitUntil(
                    () => capture.Count(N.ConnectionsRejected, Server, N.AttributeRejectReason, N.RejectTlsFailed) == 1, 8000));
            }

            CapturedMeasurement handshake = capture.Measurements(N.TlsHandshakeDuration, Server).Single();
            Assert.AreNotEqual(N.OutcomeSuccess, handshake.Tag(N.AttributeOutcome));
            Assert.IsNotNull(handshake.Tag(N.AttributeErrorType));
            Assert.AreEqual(1, capture.Count(N.Errors, Server, N.AttributeOperation, N.OperationTlsHandshake));
            Assert.AreEqual(ActivityStatusCode.Error, capture.Spans(N.SpanTlsHandshake, Server).Single().Status);
            Assert.AreEqual(0, capture.Count(N.ConnectionsOpened, Server));
            server.Stop();
        }

        #endregion

        #region Best-Effort

        [TestMethod]
        public async Task NoListener_TrafficWorksAndNothingThrows()
        {
            using SimpleTcpServer server = new SimpleTcpServer(LoopbackIp, 0);
            TaskCompletionSource<bool> received = NewSignal();
            server.Events.DataReceived += (_, _) => received.TrySetResult(true);
            server.Start();

            using SimpleTcpClient client = new SimpleTcpClient(LoopbackIp, server.Port);
            client.Connect();
            client.Send("unobserved");
            await client.SendAsync("unobserved");
            await WithTimeout(received.Task);
            client.Disconnect();
            server.Stop();
        }

        [TestMethod]
        public async Task Disabled_EmitsNothingForThatInstance()
        {
            using TelemetryCapture capture = new TelemetryCapture();
            TaskCompletionSource<bool> received = NewSignal();

            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Settings.Telemetry.Enable = false;
            server.Events.DataReceived += (_, _) => received.TrySetResult(true);
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
            client.Settings.Telemetry.EnableMetrics = false;
            client.Settings.Telemetry.EnableTraces = false;
            client.Connect();
            client.Send("quiet");
            await WithTimeout(received.Task);
            client.Disconnect();
            server.Stop();
            await Task.Delay(200);

            Assert.AreEqual(0, capture.Measurements(N.ConnectionsOpened).Count + capture.Measurements(N.SendDuration).Count + capture.Measurements(N.ListenersActive).Count);
            Assert.AreEqual(0, capture.Spans(N.SpanAccept).Count + capture.Spans(N.SpanConnect).Count + capture.Spans(N.SpanSend).Count);
        }

        [TestMethod]
        public async Task ThrowingListeners_NeverBreakNetworking()
        {
            using TelemetryCapture capture = new TelemetryCapture(throwFromCallbacks: true);
            TaskCompletionSource<bool> serverReceived = NewSignal();
            TaskCompletionSource<bool> clientReceived = NewSignal();

            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            server.Events.DataReceived += (sender, e) =>
            {
                ((SimpleTcpServer)sender!).Send(e.IpPort, "pong");
                serverReceived.TrySetResult(true);
            };
            server.Start();

            using SimpleTcpClient client = capture.Configure(new SimpleTcpClient(LoopbackIp, server.Port));
            client.Events.DataReceived += (_, _) => clientReceived.TrySetResult(true);
            client.Connect();
            client.Send("ping");
            await client.SendAsync("ping");

            await WithTimeout(serverReceived.Task);
            await WithTimeout(clientReceived.Task);
            Assert.IsTrue(client.IsConnected);
            Assert.IsTrue(capture.Count(N.SentBytes) > 0, "measurements were delivered to the listener before it threw");

            client.Disconnect();
            server.Stop();
        }

        #endregion

        #region Private-Methods

        private static async Task AssertRejected(Action<SimpleTcpServer> configure, string reason, ActivityStatusCode expectedStatus)
        {
            using TelemetryCapture capture = new TelemetryCapture();
            using SimpleTcpServer server = capture.Configure(new SimpleTcpServer(LoopbackIp, 0));
            configure(server);
            server.Start();

            using TcpClient raw = new TcpClient();
            await raw.ConnectAsync(LoopbackIp, server.Port);

            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Count(N.ConnectionsRejected, Server, N.AttributeRejectReason, reason) == 1));
            Assert.IsTrue(await TelemetryCapture.WaitUntil(() => capture.Spans(N.SpanAccept, Server).Count == 1));
            Assert.AreEqual(1, capture.Count(N.AcceptDuration, Server, N.AttributeOutcome, N.OutcomeRejected));
            Assert.AreEqual(0, capture.Count(N.ConnectionsOpened, Server));

            Activity accept = capture.Spans(N.SpanAccept, Server).Single();
            Assert.AreEqual(reason, accept.GetTagItem(N.AttributeRejectReason));
            Assert.AreEqual(N.OutcomeRejected, accept.GetTagItem(N.AttributeOutcome));
            Assert.AreEqual(expectedStatus, accept.Status);
            server.Stop();
        }

        private static TaskCompletionSource<bool> NewSignal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task WithTimeout(Task task, int timeoutMs = 5000)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task) Assert.Fail("Timed out waiting for the expected network event.");
            await task;
        }

        private static int UnusedPort()
        {
            TcpListener listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion
    }
}
