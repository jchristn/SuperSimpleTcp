namespace SuperSimpleTcp
{
    /// <summary>
    /// Stable public names for SuperSimpleTcp telemetry: the meter, the activity source, every metric instrument, every span, and every attribute key and value.
    /// These strings are a public contract consumed by collectors, dashboards, and alerts.  They will not change without a major version increment.
    /// Instrument names are dotted (OpenTelemetry style); a Prometheus exporter rewrites them to snake case with unit and type suffixes,
    /// for example supersimpletcp.send.duration becomes supersimpletcp_send_duration_seconds.
    /// </summary>
    public static class SimpleTcpTelemetryNames
    {
        #region Sources

        /// <summary>
        /// Name of the System.Diagnostics.Metrics.Meter that emits all SuperSimpleTcp metrics.
        /// Subscribe a collector to this name, for example settings.Sources.AddMeter("SuperSimpleTcp") in Radiant.
        /// </summary>
        public const string MeterName = "SuperSimpleTcp";

        /// <summary>
        /// Name of the System.Diagnostics.ActivitySource that emits all SuperSimpleTcp spans.
        /// Subscribe a collector to this name, for example settings.Sources.AddActivitySource("SuperSimpleTcp") in Radiant.
        /// </summary>
        public const string ActivitySourceName = "SuperSimpleTcp";

        #endregion

        #region Metrics

        /// <summary>
        /// UpDownCounter, {connection}.  Connections currently established.  Labels: role, instance, tls.
        /// </summary>
        public const string ConnectionsActive = "supersimpletcp.connections.active";

        /// <summary>
        /// UpDownCounter, {connection}.  Configured connection capacity (server MaxConnections) of every listening server.  Labels: role, instance, tls.
        /// </summary>
        public const string ConnectionsLimit = "supersimpletcp.connections.limit";

        /// <summary>
        /// Counter, {connection}.  Connections successfully established (server accepted or client connected).  Labels: role, instance, tls.
        /// </summary>
        public const string ConnectionsOpened = "supersimpletcp.connections.opened";

        /// <summary>
        /// Counter, {connection}.  Connections closed.  Labels: role, instance, tls, reason.
        /// </summary>
        public const string ConnectionsClosed = "supersimpletcp.connections.closed";

        /// <summary>
        /// Counter, {connection}.  Inbound connections rejected by the server before being established.  Labels: role, instance, tls, reject reason.
        /// </summary>
        public const string ConnectionsRejected = "supersimpletcp.connections.rejected";

        /// <summary>
        /// Histogram, seconds.  Lifetime of a connection from establishment to close.  Labels: role, instance, tls, reason.
        /// </summary>
        public const string ConnectionDuration = "supersimpletcp.connection.duration";

        /// <summary>
        /// UpDownCounter, {listener}.  Server listeners currently accepting connections.  Labels: role, instance, tls.
        /// </summary>
        public const string ListenersActive = "supersimpletcp.listeners.active";

        /// <summary>
        /// Histogram, seconds.  Server time from socket accept to the connection being ready or rejected (filtering plus TLS).  Labels: role, instance, tls, outcome.
        /// </summary>
        public const string AcceptDuration = "supersimpletcp.accept.duration";

        /// <summary>
        /// Histogram, seconds.  TLS handshake duration.  Labels: role, instance, tls, outcome, error.type (on failure).
        /// </summary>
        public const string TlsHandshakeDuration = "supersimpletcp.tls.handshake.duration";

        /// <summary>
        /// Histogram, seconds.  Duration of a single client connection attempt (TCP connect plus TLS).  Labels: role, instance, tls, outcome, error.type (on failure).
        /// </summary>
        public const string ConnectDuration = "supersimpletcp.connect.duration";

        /// <summary>
        /// Histogram, seconds.  Duration of a send operation including send-lock wait, write, and flush.  Labels: role, instance, tls, outcome, error.type (on failure).
        /// </summary>
        public const string SendDuration = "supersimpletcp.send.duration";

        /// <summary>
        /// Histogram, seconds.  Time a send spent waiting for the per-connection send lock (concurrency limiter).  Labels: role, instance, tls.
        /// </summary>
        public const string SendLockWaitDuration = "supersimpletcp.send.lock.wait.duration";

        /// <summary>
        /// Counter, bytes.  Bytes written to the network.  Labels: role, instance, tls.
        /// </summary>
        public const string SentBytes = "supersimpletcp.sent.bytes";

        /// <summary>
        /// Counter, bytes.  Bytes read from the network.  Labels: role, instance, tls.
        /// </summary>
        public const string ReceivedBytes = "supersimpletcp.received.bytes";

        /// <summary>
        /// Histogram, bytes.  Size of each segment read from the network (bounded by StreamBufferSize).  Labels: role, instance, tls.
        /// </summary>
        public const string ReceiveSegmentSize = "supersimpletcp.receive.segment.size";

        /// <summary>
        /// Histogram, seconds.  Execution time of the application's DataReceived handler.  Labels: role, instance, tls, dispatch mode, outcome, error.type (on failure).
        /// </summary>
        public const string HandlerDuration = "supersimpletcp.handler.duration";

        /// <summary>
        /// UpDownCounter, {segment}.  Received segments queued for asynchronous DataReceived dispatch and not yet handled.  Labels: role, instance, tls.
        /// </summary>
        public const string DispatchQueueDepth = "supersimpletcp.dispatch.queue.depth";

        /// <summary>
        /// Histogram, seconds.  Time a received segment waited in the asynchronous dispatch queue before its handler started.  Labels: role, instance, tls.
        /// </summary>
        public const string DispatchQueueWait = "supersimpletcp.dispatch.queue.wait";

        /// <summary>
        /// Counter, {run}.  Background monitor evaluations.  Labels: role, instance, tls, monitor, outcome.
        /// </summary>
        public const string MonitorRuns = "supersimpletcp.monitor.runs";

        /// <summary>
        /// Counter, {error}.  Errors by operation and exception type.  Labels: role, instance, tls, operation, error.type.
        /// </summary>
        public const string Errors = "supersimpletcp.errors";

        /// <summary>
        /// ObservableGauge, {info}.  Always 1; carries the library version as a label.  Labels: version.
        /// </summary>
        public const string BuildInfo = "supersimpletcp.build.info";

        #endregion

        #region Spans

        /// <summary>
        /// Span (Server kind).  Server accepting one inbound connection: filtering, TLS, registration.
        /// </summary>
        public const string SpanAccept = "supersimpletcp accept";

        /// <summary>
        /// Span (Internal kind).  TLS handshake, child of accept or connect.
        /// </summary>
        public const string SpanTlsHandshake = "supersimpletcp tls_handshake";

        /// <summary>
        /// Span (Client kind).  One client connection attempt.
        /// </summary>
        public const string SpanConnect = "supersimpletcp connect";

        /// <summary>
        /// Span (Internal kind).  ConnectWithRetries; parent of each connect attempt.
        /// </summary>
        public const string SpanConnectWithRetries = "supersimpletcp connect_with_retries";

        /// <summary>
        /// Span (Client kind).  One send operation.
        /// </summary>
        public const string SpanSend = "supersimpletcp send";

        /// <summary>
        /// Span (Consumer kind).  One segment read from the network.
        /// </summary>
        public const string SpanReceive = "supersimpletcp receive";

        /// <summary>
        /// Span (Internal kind).  Execution of the DataReceived handler for one segment; child of receive, including across the asynchronous dispatch hand-off.
        /// </summary>
        public const string SpanProcess = "supersimpletcp process";

        /// <summary>
        /// Span (Internal kind).  A connection closing, with its reason and lifetime.
        /// </summary>
        public const string SpanDisconnect = "supersimpletcp disconnect";

        #endregion

        #region Attribute-Keys

        /// <summary>
        /// Attribute key: role of the emitting component.  Values: server, client.
        /// </summary>
        public const string AttributeRole = "supersimpletcp.role";

        /// <summary>
        /// Attribute key: application-assigned instance name from Settings.Telemetry.InstanceName.
        /// </summary>
        public const string AttributeInstance = "supersimpletcp.instance";

        /// <summary>
        /// Attribute key: whether the connection uses SSL/TLS.  Values: true, false.
        /// </summary>
        public const string AttributeTls = "supersimpletcp.tls";

        /// <summary>
        /// Attribute key: outcome of an operation.  Values: success, error, canceled, timeout, rejected, not_found.
        /// </summary>
        public const string AttributeOutcome = "supersimpletcp.outcome";

        /// <summary>
        /// Attribute key: disconnect reason.  Values: normal, kicked, timeout.
        /// </summary>
        public const string AttributeReason = "supersimpletcp.disconnect.reason";

        /// <summary>
        /// Attribute key: reason a server rejected an inbound connection.  Values: not_permitted, blocked, max_connections, tls_failed, duplicate.
        /// </summary>
        public const string AttributeRejectReason = "supersimpletcp.reject.reason";

        /// <summary>
        /// Attribute key: the operation an error occurred in.  Values: accept, tls_handshake, connect, send, receive, handler, monitor, keepalive.
        /// </summary>
        public const string AttributeOperation = "supersimpletcp.operation";

        /// <summary>
        /// Attribute key: background monitor name.  Values: idle_client, idle_server, connection_lost.
        /// </summary>
        public const string AttributeMonitor = "supersimpletcp.monitor";

        /// <summary>
        /// Attribute key: DataReceived dispatch mode.  Values: sync, async.
        /// </summary>
        public const string AttributeDispatchMode = "supersimpletcp.dispatch.mode";

        /// <summary>
        /// Attribute key: library version, on the build info gauge.
        /// </summary>
        public const string AttributeVersion = "supersimpletcp.version";

        /// <summary>
        /// Span attribute key: number of payload bytes for a send or receive.
        /// </summary>
        public const string AttributeBytes = "supersimpletcp.bytes";

        /// <summary>
        /// Span attribute key: connection attempt number (1-based) within ConnectWithRetries.
        /// </summary>
        public const string AttributeAttempt = "supersimpletcp.connect.attempt";

        /// <summary>
        /// Span attribute key: connection lifetime in seconds, on the disconnect span.
        /// </summary>
        public const string AttributeConnectionDuration = "supersimpletcp.connection.duration_s";

        /// <summary>
        /// OpenTelemetry attribute key: exception or error type.  Fully qualified exception type name.
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// OpenTelemetry attribute key: transport protocol.  Always tcp.  Span only.
        /// </summary>
        public const string AttributeNetworkTransport = "network.transport";

        /// <summary>
        /// OpenTelemetry attribute key: remote peer address.  Span only.
        /// </summary>
        public const string AttributeNetworkPeerAddress = "network.peer.address";

        /// <summary>
        /// OpenTelemetry attribute key: remote peer port.  Span only.
        /// </summary>
        public const string AttributeNetworkPeerPort = "network.peer.port";

        /// <summary>
        /// OpenTelemetry attribute key: server address.  Span only.
        /// </summary>
        public const string AttributeServerAddress = "server.address";

        /// <summary>
        /// OpenTelemetry attribute key: server port.  Span only.
        /// </summary>
        public const string AttributeServerPort = "server.port";

        /// <summary>
        /// OpenTelemetry attribute key: negotiated TLS protocol version.  Span only.
        /// </summary>
        public const string AttributeTlsProtocolVersion = "tls.protocol.version";

        #endregion

        #region Attribute-Values

        /// <summary>
        /// Role value: server.
        /// </summary>
        public const string RoleServer = "server";

        /// <summary>
        /// Role value: client.
        /// </summary>
        public const string RoleClient = "client";

        /// <summary>
        /// Outcome value: the operation succeeded.
        /// </summary>
        public const string OutcomeSuccess = "success";

        /// <summary>
        /// Outcome value: the operation failed with an exception.
        /// </summary>
        public const string OutcomeError = "error";

        /// <summary>
        /// Outcome value: the operation was canceled.
        /// </summary>
        public const string OutcomeCanceled = "canceled";

        /// <summary>
        /// Outcome value: the operation timed out.
        /// </summary>
        public const string OutcomeTimeout = "timeout";

        /// <summary>
        /// Outcome value: the server rejected the connection.
        /// </summary>
        public const string OutcomeRejected = "rejected";

        /// <summary>
        /// Outcome value: the target client of a server send was not connected.
        /// </summary>
        public const string OutcomeNotFound = "not_found";

        /// <summary>
        /// Reject reason value: client IP is not in PermittedIPs.
        /// </summary>
        public const string RejectNotPermitted = "not_permitted";

        /// <summary>
        /// Reject reason value: client IP is in BlockedIPs.
        /// </summary>
        public const string RejectBlocked = "blocked";

        /// <summary>
        /// Reject reason value: MaxConnections reached.
        /// </summary>
        public const string RejectMaxConnections = "max_connections";

        /// <summary>
        /// Reject reason value: TLS handshake or validation failed.
        /// </summary>
        public const string RejectTlsFailed = "tls_failed";

        /// <summary>
        /// Reject reason value: a connection with the same IP:port is already registered.
        /// </summary>
        public const string RejectDuplicate = "duplicate";

        /// <summary>
        /// Operation value: server accept.
        /// </summary>
        public const string OperationAccept = "accept";

        /// <summary>
        /// Operation value: TLS handshake.
        /// </summary>
        public const string OperationTlsHandshake = "tls_handshake";

        /// <summary>
        /// Operation value: client connect.
        /// </summary>
        public const string OperationConnect = "connect";

        /// <summary>
        /// Operation value: send.
        /// </summary>
        public const string OperationSend = "send";

        /// <summary>
        /// Operation value: receive loop.
        /// </summary>
        public const string OperationReceive = "receive";

        /// <summary>
        /// Operation value: application DataReceived handler.
        /// </summary>
        public const string OperationHandler = "handler";

        /// <summary>
        /// Operation value: background monitor.
        /// </summary>
        public const string OperationMonitor = "monitor";

        /// <summary>
        /// Operation value: TCP keepalive configuration.
        /// </summary>
        public const string OperationKeepalive = "keepalive";

        /// <summary>
        /// Monitor value: server idle client monitor.
        /// </summary>
        public const string MonitorIdleClient = "idle_client";

        /// <summary>
        /// Monitor value: client idle server monitor.
        /// </summary>
        public const string MonitorIdleServer = "idle_server";

        /// <summary>
        /// Monitor value: client connection-lost monitor.
        /// </summary>
        public const string MonitorConnectionLost = "connection_lost";

        /// <summary>
        /// Dispatch mode value: DataReceived invoked on the receive loop.
        /// </summary>
        public const string DispatchSync = "sync";

        /// <summary>
        /// Dispatch mode value: DataReceived invoked on the ordered asynchronous dispatcher.
        /// </summary>
        public const string DispatchAsync = "async";

        #endregion
    }
}
