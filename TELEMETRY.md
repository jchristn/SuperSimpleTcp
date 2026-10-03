# SuperSimpleTcp Telemetry

SuperSimpleTcp measures itself and hands the numbers to whatever you already use to watch your systems. It emits metrics and traces through the .NET base class library, a `Meter` and an `ActivitySource` both named `SuperSimpleTcp`, and stops there. It never opens a connection to a backend, never picks a vendor, and takes no dependency on any exporter. Your host application subscribes a collector to those two names (Radiant, the OpenTelemetry SDK, `dotnet-counters`, or a two-line unit test) and the data flows to Prometheus, Tempo, or any OTLP backend.

Telemetry is on by default. When nothing is subscribed, each instrumented operation costs a few nanoseconds and no spans are allocated.

The goal is operational: an on-call engineer looking only at dashboards and traces should be able to tell **where the time went** (connect, TLS, send-lock wait, socket write, dispatch queue, your `DataReceived` handler) and **what failed** (which rejection reason, which exception type, in which operation) without reading the source or attaching a debugger.

## Contents

1. [Subscribing](#subscribing)
2. [Configuration](#configuration)
3. [Metrics catalog](#metrics-catalog)
4. [Labels](#labels)
5. [Spans catalog](#spans-catalog)
6. [Trace topology](#trace-topology)
7. [Recommended PromQL and alerts](#recommended-promql-and-alerts)
8. [Dashboard map](#dashboard-map)
9. [Design rules and limits](#design-rules-and-limits)

## Subscribing

The strings `SuperSimpleTcp` (meter) and `SuperSimpleTcp` (activity source) are the entire contract. They are also available as `SimpleTcpTelemetryNames.MeterName` and `SimpleTcpTelemetryNames.ActivitySourceName`.

With [Radiant](https://github.com/jchristn/Radiant):

```csharp
RadiantSettings settings = new RadiantSettings("my-service");
settings.Otlp.Endpoint = "http://127.0.0.1:4317";
settings.Prometheus.Enable = true;
settings.Sources.AddMeter(SimpleTcpTelemetryNames.MeterName);
settings.Sources.AddActivitySource(SimpleTcpTelemetryNames.ActivitySourceName);

using (RadiantHost host = RadiantHost.Start(settings))
{
    // start your SimpleTcpServer / SimpleTcpClient and run the app
}
```

With the OpenTelemetry SDK:

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using MeterProvider meters = Sdk.CreateMeterProviderBuilder()
    .AddMeter("SuperSimpleTcp")
    .AddRuntimeInstrumentation()       // recommended: GC, thread pool, exceptions
    .AddOtlpExporter()                 // or AddPrometheusHttpListener()
    .Build();

using TracerProvider tracer = Sdk.CreateTracerProviderBuilder()
    .AddSource("SuperSimpleTcp")
    .AddOtlpExporter()
    .Build();
```

From the command line, against a running process:

```
dotnet-counters monitor -n MyApp --counters SuperSimpleTcp
```

Runtime metrics (GC, thread pool, lock contention) are a host concern. Radiant includes them by default (`Metrics.IncludeRuntime`); with the OpenTelemetry SDK add `OpenTelemetry.Instrumentation.Runtime`. Thread-pool starvation shows up in SuperSimpleTcp as rising `dispatch.queue.wait` and `send.lock.wait.duration`, so it is worth having both on the same dashboard.

On .NET 8 and later the BCL types are in-box. On `netstandard2.1`, `net461`, `net462`, and `net48` the package depends on `System.Diagnostics.DiagnosticSource` 8.0.1.

## Configuration

Each server and client has its own `Settings.Telemetry` (`SimpleTcpTelemetrySettings`). Configure it before calling `Start()` or `Connect()`.

| Property | Default | Meaning |
| --- | --- | --- |
| `Enable` | `true` | Master switch for this instance. `false` emits no metrics and no spans. |
| `EnableMetrics` | `true` | Emit metrics on the `SuperSimpleTcp` meter. |
| `EnableTraces` | `true` | Emit spans on the `SuperSimpleTcp` activity source. Spans are only created when a listener samples them. |
| `InstanceName` | `"default"` | Value of the `supersimpletcp.instance` label. 1 to 64 characters. Use a small fixed set (for example `ingest`, `control`) to tell several servers or clients in one process apart. Never use per-connection values. |

```csharp
SimpleTcpServer server = new SimpleTcpServer("0.0.0.0:9000");
server.Settings.Telemetry.InstanceName = "ingest";
server.Settings.Telemetry.EnableTraces = false;   // metrics only, for very high message rates
server.Start();
```

## Metrics catalog

Instrument names are dotted (OpenTelemetry style). A Prometheus exporter rewrites them to snake case and appends unit and type suffixes; the last column shows the result with the OpenTelemetry .NET Prometheus exporter. Label keys are rewritten the same way (`supersimpletcp.role` becomes `supersimpletcp_role`, `error.type` becomes `error_type`).

Every metric carries the base labels `supersimpletcp.role`, `supersimpletcp.instance`, and `supersimpletcp.tls`, except `build.info`.

| Instrument | Type | Unit | Extra labels | Description | Prometheus name |
| --- | --- | --- | --- | --- | --- |
| `supersimpletcp.connections.active` | UpDownCounter | `{connection}` | | Connections currently established. | `supersimpletcp_connections_active` |
| `supersimpletcp.connections.limit` | UpDownCounter | `{connection}` | | `MaxConnections` of every listening server (server role only). Divide `active` by this for utilization. | `supersimpletcp_connections_limit` |
| `supersimpletcp.connections.opened` | Counter | `{connection}` | | Connections established (server accepted, or client connected). | `supersimpletcp_connections_opened_total` |
| `supersimpletcp.connections.closed` | Counter | `{connection}` | `disconnect.reason` | Connections closed. | `supersimpletcp_connections_closed_total` |
| `supersimpletcp.connections.rejected` | Counter | `{connection}` | `reject.reason` | Inbound connections the server refused before establishing them. | `supersimpletcp_connections_rejected_total` |
| `supersimpletcp.connection.duration` | Histogram | `s` | `disconnect.reason` | Connection lifetime from establishment to close. | `supersimpletcp_connection_duration_seconds` |
| `supersimpletcp.listeners.active` | UpDownCounter | `{listener}` | | Server listeners currently accepting connections. | `supersimpletcp_listeners_active` |
| `supersimpletcp.accept.duration` | Histogram | `s` | `outcome` | Server time from socket accept to ready or rejected: IP filtering, capacity check, TLS. | `supersimpletcp_accept_duration_seconds` |
| `supersimpletcp.tls.handshake.duration` | Histogram | `s` | `outcome`, `error.type` | TLS handshake, both roles. Count by outcome for the failure ratio. | `supersimpletcp_tls_handshake_duration_seconds` |
| `supersimpletcp.connect.duration` | Histogram | `s` | `outcome`, `error.type` | One client connection attempt (TCP connect plus TLS). Each `ConnectWithRetries` attempt is recorded. | `supersimpletcp_connect_duration_seconds` |
| `supersimpletcp.send.duration` | Histogram | `s` | `outcome`, `error.type` | One send call: send-lock wait, write, flush. | `supersimpletcp_send_duration_seconds` |
| `supersimpletcp.send.lock.wait.duration` | Histogram | `s` | | Time a send waited for the per-connection send lock (concurrent senders on one connection queue here). | `supersimpletcp_send_lock_wait_duration_seconds` |
| `supersimpletcp.sent.bytes` | Counter | `By` | | Bytes written to the network. | `supersimpletcp_sent_bytes_total` |
| `supersimpletcp.received.bytes` | Counter | `By` | | Bytes read from the network. | `supersimpletcp_received_bytes_total` |
| `supersimpletcp.receive.segment.size` | Histogram | `By` | | Size of each read. Segments pinned at `StreamBufferSize` mean the buffer is the limit. | `supersimpletcp_receive_segment_size_bytes` |
| `supersimpletcp.handler.duration` | Histogram | `s` | `dispatch.mode`, `outcome`, `error.type` | Execution time of your `DataReceived` handler. | `supersimpletcp_handler_duration_seconds` |
| `supersimpletcp.dispatch.queue.depth` | UpDownCounter | `{segment}` | | Segments waiting for the asynchronous `DataReceived` worker. Growth means handlers are slower than arrivals. | `supersimpletcp_dispatch_queue_depth` |
| `supersimpletcp.dispatch.queue.wait` | Histogram | `s` | | Time a segment waited in the asynchronous dispatch queue before its handler started. | `supersimpletcp_dispatch_queue_wait_seconds` |
| `supersimpletcp.monitor.runs` | Counter | `{run}` | `monitor`, `outcome` | Background monitor ticks. A rate of zero while connections exist means a monitor stopped. | `supersimpletcp_monitor_runs_total` |
| `supersimpletcp.errors` | Counter | `{error}` | `operation`, `error.type` | Every recorded failure, by operation and exception type. | `supersimpletcp_errors_total` |
| `supersimpletcp.build.info` | ObservableGauge | `{info}` | `supersimpletcp.version` only | Always 1. Appears once the library has been used in the process. | `supersimpletcp_build_info` |

Histogram bucket boundaries are a collector concern. For the duration histograms, buckets from 0.0005 s to 10 s suit LAN traffic. Quantiles are derived in Prometheus or Grafana from the buckets; nothing is precomputed in process.

The existing `Statistics` object (`SentBytes`, `ReceivedBytes`, `UpTime`) is unchanged and still available for in-process use. The metrics above are independent of it, and `Statistics.Reset()` does not affect them.

## Labels

All metric labels are bounded. Peer addresses, ports, and payloads never appear on metrics.

| Key | Values |
| --- | --- |
| `supersimpletcp.role` | `server`, `client` |
| `supersimpletcp.instance` | `Settings.Telemetry.InstanceName` (default `default`) |
| `supersimpletcp.tls` | `true`, `false` |
| `supersimpletcp.outcome` | `success`, `error`, `canceled`, `timeout`, `rejected`, `not_found` |
| `supersimpletcp.disconnect.reason` | `normal`, `kicked`, `timeout` |
| `supersimpletcp.reject.reason` | `not_permitted`, `blocked`, `max_connections`, `tls_failed`, `duplicate` |
| `supersimpletcp.operation` | `accept`, `tls_handshake`, `connect`, `send`, `receive`, `handler`, `monitor`, `keepalive` |
| `supersimpletcp.monitor` | `idle_client` (server), `idle_server` (client), `connection_lost` (client) |
| `supersimpletcp.dispatch.mode` | `sync`, `async` |
| `error.type` | Fully qualified exception type name, for example `System.Net.Sockets.SocketException` |

Outcome meanings by operation:

- **send**: `success`; `error` (exception thrown to the caller); `canceled` (async send canceled, which is not thrown); `not_found` (server send to an `IpPort` that is not connected, which returns silently).
- **connect / tls_handshake**: `success`, `error`, `timeout`, `canceled`.
- **accept**: `success`, `rejected` (see `reject.reason`), `error`.
- **handler**: `success`, `error`.
- **monitor.runs**: `success`, `error`.

## Spans catalog

All spans come from the `SuperSimpleTcp` activity source and carry `supersimpletcp.role`, `supersimpletcp.instance`, `supersimpletcp.tls` (boolean), and `network.transport=tcp`. Status is set explicitly: `Ok` on success, `Error` on failure with an `exception` event (`exception.type`, `exception.message`, `exception.stacktrace`) and `error.type`.

| Span name | Kind | Emitted by | Attributes | Status rules |
| --- | --- | --- | --- | --- |
| `supersimpletcp accept` | Server | server, per inbound connection | `network.peer.address`, `network.peer.port`, `server.address`, `server.port`, `supersimpletcp.outcome`, `supersimpletcp.reject.reason` | `Ok` when established. Policy rejections (`not_permitted`, `blocked`) leave status unset. `max_connections`, `tls_failed`, `duplicate` set `Error`. |
| `supersimpletcp tls_handshake` | Internal | both; child of `accept` or `connect` | `tls.protocol.version` (for example `1.2`), `supersimpletcp.outcome` | `Error` with exception on failure or timeout. |
| `supersimpletcp connect` | Client | client, per attempt | `server.address`, `server.port`, `supersimpletcp.connect.attempt` (within retries), `supersimpletcp.outcome` | `Error` with exception on refusal, timeout, or TLS failure. |
| `supersimpletcp connect_with_retries` | Internal | client `ConnectWithRetries` | `server.address`, `server.port`, `supersimpletcp.connect.attempt` (attempts made) | `Error` with the final `TimeoutException` when all attempts fail. |
| `supersimpletcp send` | Client | both, per send call | peer (server role) or `server.address`/`server.port` (client role), `supersimpletcp.bytes`, `supersimpletcp.outcome` | `Error` on exception or `not_found`. `canceled` leaves status unset. |
| `supersimpletcp receive` | Consumer | both, per segment read | peer, `supersimpletcp.bytes` | `Error` if a synchronous handler threw. |
| `supersimpletcp process` | Internal | both, per `DataReceived` invocation; child of `receive` | peer, `supersimpletcp.dispatch.mode` | `Error` with exception if your handler threw. |
| `supersimpletcp disconnect` | Internal | both, per closed connection | peer, `supersimpletcp.disconnect.reason`, `supersimpletcp.connection.duration_s` | `Error` with exception if the receive loop ended on an unexpected exception. |

Peer addresses and ports appear on spans only. No payload bytes are ever recorded.

## Trace topology

```
host span (your code)                      new trace per inbound connection
  └─ supersimpletcp connect_with_retries     supersimpletcp accept   (Server, root)
       ├─ supersimpletcp connect  #1           └─ supersimpletcp tls_handshake
       └─ supersimpletcp connect  #2
            └─ supersimpletcp tls_handshake

host span (your code)                      new trace per received segment
  └─ supersimpletcp send   (Client)          supersimpletcp receive  (Consumer, root)
                                               └─ supersimpletcp process
                                                    └─ your spans inside DataReceived

                                           supersimpletcp disconnect (root)
```

- Calls you make (`Connect`, `ConnectAsync`, `ConnectWithRetries`, `Send`, `SendAsync`) join whatever `Activity.Current` is at the call site, so they nest under your request or job span.
- Work the library starts on its own (accept loop, receive loops, idle and connection monitors, the asynchronous dispatch worker) clears the ambient span first. Accept, receive, and disconnect spans therefore start new traces instead of nesting forever under whatever span was current when `Start()` or `Connect()` ran.
- The asynchronous `DataReceived` hand-off propagates context explicitly: the `receive` span's context travels with the queued segment and the worker starts `process` as its child. `Activity.Current` inside your handler is the `process` span, so your own spans and `ILogger` scopes correlate automatically.
- SuperSimpleTcp does not frame messages, so it cannot carry W3C `traceparent` across the wire. If your protocol has a header, write `Activity.Current.Id` into it on send and pass it as the parent of your own span inside `DataReceived` to link the two processes.

## Recommended PromQL and alerts

Panels:

```promql
# Active connections and utilization by instance
sum by (supersimpletcp_instance, supersimpletcp_role) (supersimpletcp_connections_active)
sum by (supersimpletcp_instance) (supersimpletcp_connections_active{supersimpletcp_role="server"})
  / sum by (supersimpletcp_instance) (supersimpletcp_connections_limit)

# Connection churn and close reasons
sum by (supersimpletcp_disconnect_reason) (rate(supersimpletcp_connections_closed_total[5m]))

# Rejections by reason
sum by (supersimpletcp_reject_reason) (rate(supersimpletcp_connections_rejected_total[5m]))

# Throughput
sum by (supersimpletcp_role) (rate(supersimpletcp_sent_bytes_total[1m]))
sum by (supersimpletcp_role) (rate(supersimpletcp_received_bytes_total[1m]))

# Where send time goes: total vs lock wait
histogram_quantile(0.95, sum by (le, supersimpletcp_role) (rate(supersimpletcp_send_duration_seconds_bucket[5m])))
histogram_quantile(0.95, sum by (le, supersimpletcp_role) (rate(supersimpletcp_send_lock_wait_duration_seconds_bucket[5m])))

# Where receive time goes: queue wait vs handler
histogram_quantile(0.95, sum by (le) (rate(supersimpletcp_dispatch_queue_wait_seconds_bucket[5m])))
histogram_quantile(0.95, sum by (le, supersimpletcp_dispatch_mode) (rate(supersimpletcp_handler_duration_seconds_bucket[5m])))

# Connect and TLS latency
histogram_quantile(0.95, sum by (le) (rate(supersimpletcp_connect_duration_seconds_bucket[5m])))
histogram_quantile(0.95, sum by (le, supersimpletcp_role) (rate(supersimpletcp_tls_handshake_duration_seconds_bucket[5m])))

# Errors by operation and type
sum by (supersimpletcp_operation, error_type) (rate(supersimpletcp_errors_total[5m]))
```

Alerts (tune thresholds to your traffic):

```yaml
groups:
  - name: supersimpletcp
    rules:
      - alert: SuperSimpleTcpCapacityRejections
        expr: sum by (supersimpletcp_instance) (increase(supersimpletcp_connections_rejected_total{supersimpletcp_reject_reason="max_connections"}[5m])) > 0
        labels: { severity: warning }
        annotations: { summary: "Server {{ $labels.supersimpletcp_instance }} is refusing connections at MaxConnections" }

      - alert: SuperSimpleTcpNearCapacity
        expr: |
          sum by (supersimpletcp_instance) (supersimpletcp_connections_active{supersimpletcp_role="server"})
            / sum by (supersimpletcp_instance) (supersimpletcp_connections_limit) > 0.8
        for: 10m
        labels: { severity: warning }

      - alert: SuperSimpleTcpTlsHandshakeFailures
        expr: |
          sum by (supersimpletcp_instance, supersimpletcp_role) (rate(supersimpletcp_tls_handshake_duration_seconds_count{supersimpletcp_outcome!="success"}[5m]))
            / sum by (supersimpletcp_instance, supersimpletcp_role) (rate(supersimpletcp_tls_handshake_duration_seconds_count[5m])) > 0.1
        for: 10m
        labels: { severity: warning }

      - alert: SuperSimpleTcpSendErrors
        expr: sum by (supersimpletcp_instance, supersimpletcp_role) (rate(supersimpletcp_send_duration_seconds_count{supersimpletcp_outcome="error"}[5m])) > 0
        for: 5m
        labels: { severity: warning }

      - alert: SuperSimpleTcpSlowSends
        expr: histogram_quantile(0.95, sum by (le, supersimpletcp_instance) (rate(supersimpletcp_send_duration_seconds_bucket[5m]))) > 0.5
        for: 10m
        labels: { severity: warning }

      - alert: SuperSimpleTcpDispatchBacklog
        expr: max by (supersimpletcp_instance, supersimpletcp_role) (supersimpletcp_dispatch_queue_depth) > 1000
        for: 5m
        labels: { severity: critical }
        annotations: { summary: "DataReceived handlers are not keeping up; memory will grow" }

      - alert: SuperSimpleTcpHandlerErrors
        expr: sum by (supersimpletcp_instance, error_type) (increase(supersimpletcp_errors_total{supersimpletcp_operation="handler"}[5m])) > 0
        labels: { severity: warning }
        annotations: { summary: "DataReceived handler threw {{ $labels.error_type }}" }

      - alert: SuperSimpleTcpClientConnectFailures
        expr: sum by (supersimpletcp_instance) (rate(supersimpletcp_connect_duration_seconds_count{supersimpletcp_outcome!="success"}[5m])) > 0
        for: 10m
        labels: { severity: warning }

      - alert: SuperSimpleTcpListenerDown
        expr: sum by (job, supersimpletcp_instance) (supersimpletcp_listeners_active) == 0
        for: 2m
        labels: { severity: critical }

      - alert: SuperSimpleTcpMonitorStalled
        expr: |
          sum by (job, supersimpletcp_instance, supersimpletcp_role) (rate(supersimpletcp_monitor_runs_total[5m])) == 0
            and sum by (job, supersimpletcp_instance, supersimpletcp_role) (supersimpletcp_connections_active) > 0
        for: 5m
        labels: { severity: warning }
```

`SuperSimpleTcpHandlerErrors` deserves attention. An exception thrown by your `DataReceived` handler means that segment was not processed. The library catches it and keeps delivering later segments (since v3.2.1; earlier versions stopped the asynchronous dispatch worker), but the data in that segment is lost to your application. With synchronous dispatch the exception still ends the receive loop and the connection closes. Catch exceptions inside your handler.

## Dashboard map

SuperSimpleTcp is a library, so it ships no Grafana dashboards or compose stack; those belong to the service that embeds it. A TCP-focused dashboard, or a "TCP" domain dashboard inside your product folder, works well with these rows:

| Row | Panels | Answers |
| --- | --- | --- |
| Overview | listeners active, connections active vs limit, opened/closed rate, error rate by operation | Is it up, how loaded, is anything failing? |
| Connections | close reasons, rejections by reason, connection duration p50/p95, accept duration p95 | Who is being turned away, and why? |
| Throughput | bytes sent/received per second, segment size distribution | How much traffic, and is the buffer the limit? |
| Latency | send p95 vs send-lock wait p95, connect p95, TLS handshake p95 | Is the time in the lock, the socket, or the handshake? |
| Receive path | dispatch queue depth, queue wait p95, handler p95 by mode, handler errors | Are application handlers keeping up? |
| Errors | `errors_total` by operation and `error_type`, TLS failure ratio | What failed, exactly? |

Link the panels to Tempo with a span search on `resource.service.name` plus `name="supersimpletcp send"` (or `accept`, `connect`) to jump from a slow percentile to an example trace.

## Design rules and limits

- **Best-effort.** Every metric write and span operation is wrapped so that a throwing listener, exporter, or sampler can never affect networking. A test proves traffic still flows when listeners throw from measurement, span-start, and span-stop callbacks.
- **No new behavior.** Instrumentation records outcomes without changing control flow: exceptions that were thrown before are still thrown, cancellations that were swallowed are still swallowed.
- **Shutdown is not an error.** The `SocketException` raised when `Stop()` aborts the pending accept is not counted.
- **Cardinality.** Labels are limited to the fixed sets above plus `InstanceName`, which you control.
- **Overhead.** With nothing subscribed, recording is a settings check plus `Stopwatch.GetTimestamp()` calls, and spans are not created. With traces sampled at 100%, every segment produces a `receive` and a `process` span; at very high message rates use a ratio sampler or set `EnableTraces = false`.
- **Not covered.** Bytes are counted per send and per read, not per application message (the library has no framing). Cross-process trace propagation requires a header in your own protocol (see [Trace topology](#trace-topology)). TCP keepalive configuration failures are counted in `errors{operation="keepalive"}` but are platform-dependent and not exercised by the test suite.
- **net461.** `System.Diagnostics.DiagnosticSource` 8.x is not officially tested by Microsoft on .NET Framework 4.6.1 (out of support), and consumers targeting net461 will see an informational build warning. net462 and later are fully supported.
