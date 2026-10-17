namespace Test.Shared.Suites;

using System.Net;
using System.Net.Sockets;
using System.Text;
using SuperSimpleTcp;
using Touchstone.Core;

internal static partial class RuntimeSuites
{
    public static TestSuiteDescriptor ReceiveRegressionSuite()
    {
        const string suite = "ReceiveRegression";
        List<TestCaseDescriptor> cases = new();
        cases.Add(TestCaseFactory.Async(suite, "IdleClientsReleaseWorkers",
            "Idle clients do not occupy one thread-pool worker per connection (#237)",
            async token =>
            {
                // Sample on a dedicated thread so the observer neither consumes a pool
                // worker nor depends on the pool it is measuring. Do not alter pool limits.
                Task<int> SampleAsync() => Task.Factory.StartNew(() =>
                {
                    int minimum = int.MaxValue;
                    for (int i = 0; i < 20; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        ThreadPool.GetMaxThreads(out int maximum, out _);
                        ThreadPool.GetAvailableThreads(out int available, out _);
                        minimum = Math.Min(minimum, maximum - available);
                        Thread.Sleep(50);
                    }
                    return minimum;
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                int baseline = await SampleAsync().ConfigureAwait(false);
                using TcpListener listener = new(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                List<SimpleTcpClient> clients = new();
                List<TcpClient> peers = new();
                try
                {
                    for (int i = 0; i < 8; i++)
                    {
                        SimpleTcpClient client = new(TestEnvironment.LoopbackIp, port);
                        clients.Add(client);
                        client.Connect();
                        peers.Add(await listener.AcceptTcpClientAsync(token).ConfigureAwait(false));
                    }
                    await Task.Delay(250, token).ConfigureAwait(false);
                    int busy = await SampleAsync().ConfigureAwait(false);
                    TestAssert.True(busy < baseline + 4,
                        $"Eight idle clients occupied workers: baseline={baseline}, connected={busy}.");
                    TestAssert.True(clients.All(c => c.IsConnected), "Idle clients should remain connected.");
                }
                finally
                {
                    foreach (SimpleTcpClient client in clients) client.Dispose();
                    foreach (TcpClient peer in peers) peer.Dispose();
                }
            }));

        foreach (bool tls in new[] { false, true })
        {
            string transport = tls ? "Tls" : "Tcp";
            foreach (bool asyncEvents in new[] { false, true })
            {
                cases.Add(TestCaseFactory.Async(suite, $"{transport}DelayedReceive_{asyncEvents}",
                    "Data arriving after multiple legacy read timeouts is delivered once and in order",
                    async token =>
                    {
                        using SimpleTcpServer server = CreateReceiveServer(tls);
                        server.Start();
                        using SimpleTcpClient client = CreateReceiveClient(server.Port, tls);
                        client.Settings.ReadTimeoutMs = 10;
                        client.Settings.UseAsyncDataReceivedEvents = asyncEvents;
                        List<byte> received = new();
                        TaskCompletionSource<bool> first = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        TaskCompletionSource<bool> all = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        byte[] expected = Encoding.UTF8.GetBytes("first-second");
                        client.Events.DataReceived += (_, args) =>
                        {
                            lock (received)
                            {
                                received.AddRange(args.Data);
                                if (received.Count >= 6) first.TrySetResult(true);
                                if (received.Count >= expected.Length) all.TrySetResult(true);
                            }
                        };
                        client.Connect();
                        await Task.Delay(150, token).ConfigureAwait(false);
                        TestAssert.True(client.IsConnected, "ReadTimeoutMs must not disconnect an idle client.");
                        lock (received) TestAssert.Equal(0, received.Count, "Idle reads must not raise data events.");
                        string id = TestEnvironment.GetSingleClient(server);
                        server.Send(id, "first-");
                        await TestEnvironment.WithTimeoutAsync(first.Task, TimeSpan.FromSeconds(5), "First data was not received.");
                        await Task.Delay(150, token).ConfigureAwait(false);
                        server.Send(id, "second");
                        await TestEnvironment.WithTimeoutAsync(all.Task, TimeSpan.FromSeconds(5), "Later data was not received.");
                        await Task.Delay(100, token).ConfigureAwait(false);
                        lock (received) TestAssert.SequenceEqual(expected, received, "Data was lost, duplicated, or reordered.");
                        await client.DisconnectAsync().ConfigureAwait(false);
                    }));
            }

            cases.Add(TestCaseFactory.Async(suite, $"{transport}TrafficResetsIdleTimeout",
                "Received traffic resets the idle deadline and silence subsequently disconnects",
                async token =>
                {
                    using SimpleTcpServer server = CreateReceiveServer(tls);
                    server.Start();
                    using SimpleTcpClient client = CreateReceiveClient(server.Port, tls);
                    client.Settings.IdleServerTimeoutMs = 1500;
                    client.Settings.IdleServerEvaluationIntervalMs = 25;
                    TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    int received = 0;
                    client.Events.DataReceived += (_, args) => Interlocked.Add(ref received, args.Data.Count);
                    client.Events.Disconnected += (_, args) => disconnected.TrySetResult(args);
                    client.Connect();
                    await TestEnvironment.WaitForConditionAsync(() => server.Connections == 1,
                        TimeSpan.FromSeconds(2), "Server did not register the connection.", token).ConfigureAwait(false);
                    string id = TestEnvironment.GetSingleClient(server);
                    // Continue longer than the original idle deadline, acknowledging each send.
                    for (int i = 1; i <= 5; i++)
                    {
                        await Task.Delay(400, token).ConfigureAwait(false);
                        server.Send(id, "x");
                        int expected = i;
                        await TestEnvironment.WaitForConditionAsync(() => Volatile.Read(ref received) == expected,
                            TimeSpan.FromSeconds(1), "Keepalive data was not received.", token).ConfigureAwait(false);
                        TestAssert.True(client.IsConnected, "Receiving data must reset the idle deadline.");
                        TestAssert.False(disconnected.Task.IsCompleted, "Active server was incorrectly timed out.");
                    }
                    ConnectionEventArgs result = await TestEnvironment.WithTimeoutAsync(disconnected.Task,
                        TimeSpan.FromSeconds(5), "Silence after traffic must eventually time out.");
                    TestAssert.Equal(DisconnectReason.Timeout, result.Reason, "Silence should cause a timeout.");
                }));

            foreach (string action in new[] { "Disconnect", "DisconnectAsync", "Dispose", "RemoteClose", "IdleTimeout" })
            {
                cases.Add(TestCaseFactory.Async(suite, $"{transport}PendingRead_{action}",
                    "Closing an idle transport completes its pending receive and reports the correct reason",
                    async token =>
                    {
                        using SimpleTcpServer server = CreateReceiveServer(tls);
                        server.Start();
                        using SimpleTcpClient client = CreateReceiveClient(server.Port, tls);
                        int dataEvents = 0;
                        int disconnectEvents = 0;
                        TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        client.Events.DataReceived += (_, _) => Interlocked.Increment(ref dataEvents);
                        client.Events.Disconnected += (_, args) =>
                        {
                            Interlocked.Increment(ref disconnectEvents);
                            disconnected.TrySetResult(args);
                        };
                        if (action == "IdleTimeout")
                        {
                            client.Settings.IdleServerTimeoutMs = 250;
                            client.Settings.IdleServerEvaluationIntervalMs = 25;
                        }
                        client.Connect();
                        await Task.Delay(100, token).ConfigureAwait(false);
                        switch (action)
                        {
                            case "Disconnect": client.Disconnect(); break;
                            case "DisconnectAsync": await client.DisconnectAsync().ConfigureAwait(false); break;
                            case "Dispose": client.Dispose(); break;
                            case "RemoteClose": server.DisconnectClient(TestEnvironment.GetSingleClient(server)); break;
                        }
                        ConnectionEventArgs result = await TestEnvironment.WithTimeoutAsync(disconnected.Task,
                            TimeSpan.FromSeconds(5), "Pending receive did not detect teardown.").ConfigureAwait(false);
                        TestAssert.Equal(action == "IdleTimeout" ? DisconnectReason.Timeout : DisconnectReason.Normal,
                            result.Reason, "Unexpected disconnect reason.");
                        await Task.Delay(100, token).ConfigureAwait(false);
                        TestAssert.False(client.IsConnected, "Client should be disconnected.");
                        TestAssert.Equal(1, Volatile.Read(ref disconnectEvents), "Disconnect must be reported once.");
                        TestAssert.Equal(0, Volatile.Read(ref dataEvents), "Closing an idle read must not produce data.");
                    }));
            }
        }

        foreach (bool reset in new[] { false, true })
        {
            cases.Add(TestCaseFactory.Async(suite, reset ? "PeerReset" : "PeerFin",
                "An idle asynchronous read terminates on peer FIN or reset without delivering data",
                async token =>
                {
                    using TcpListener listener = new(IPAddress.Loopback, 0);
                    listener.Start();
                    using SimpleTcpClient client = new(TestEnvironment.LoopbackIp, ((IPEndPoint)listener.LocalEndpoint).Port);
                    int dataEvents = 0;
                    TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    client.Events.DataReceived += (_, _) => Interlocked.Increment(ref dataEvents);
                    client.Events.Disconnected += (_, args) => disconnected.TrySetResult(args);
                    client.Connect();
                    using TcpClient peer = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    await Task.Delay(100, token).ConfigureAwait(false);
                    if (reset) peer.Client.LingerState = new LingerOption(true, 0);
                    else peer.Client.Shutdown(SocketShutdown.Send);
                    if (reset) peer.Close();
                    ConnectionEventArgs result = await TestEnvironment.WithTimeoutAsync(disconnected.Task,
                        TimeSpan.FromSeconds(5), "Peer closure did not complete the pending receive.");
                    TestAssert.Equal(DisconnectReason.Normal, result.Reason, "Peer closure should be normal.");
                    TestAssert.Equal(0, Volatile.Read(ref dataEvents), "Peer closure must not deliver empty data.");
                    TestAssert.False(client.IsConnected, "Peer closure must disconnect the client.");
                }));
        }
        return new TestSuiteDescriptor(suite, "Receive regression (#237)", cases);
    }

    private static SimpleTcpServer CreateReceiveServer(bool tls)
    {
        return tls
            ? new SimpleTcpServer(TestEnvironment.LoopbackIp, 0, true, TestEnvironment.CertificatePath, "simpletcp")
            : new SimpleTcpServer(TestEnvironment.LoopbackIp, 0);
    }

    private static SimpleTcpClient CreateReceiveClient(int port, bool tls)
    {
        SimpleTcpClient client = tls
            ? new SimpleTcpClient(TestEnvironment.LoopbackIp, port, true, TestEnvironment.CertificatePath, "simpletcp")
            : new SimpleTcpClient(TestEnvironment.LoopbackIp, port);
        client.Settings.AcceptInvalidCertificates = true;
        client.Settings.MutuallyAuthenticate = false;
        return client;
    }
}
