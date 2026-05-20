namespace Test.Shared.Suites;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using SuperSimpleTcp;

using Touchstone.Core;

internal static partial class RuntimeSuites
{
    public static TestSuiteDescriptor ConnectivitySuite()
    {
        const string suiteId = "Connectivity";

        return new TestSuiteDescriptor(
            suiteId,
            "Connectivity",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Async(
                    suiteId,
                    "ServerStartStop",
                    "SimpleTcpServer.Start and Stop toggle listening state",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();
                        await TestEnvironment.WaitForConditionAsync(
                            () => server.IsListening,
                            TimeSpan.FromSeconds(2),
                            "Server never entered the listening state.",
                            token).ConfigureAwait(false);

                        StopServer(server);
                        TestAssert.False(server.IsListening, "Server should stop listening after Stop().");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ServerStartTwiceThrows",
                    "SimpleTcpServer.Start rejects duplicate starts",
                    () =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();
                        TestAssert.Throws<InvalidOperationException>(() => server.Start());
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConnectDisconnect",
                    "SimpleTcpClient.Connect and Disconnect manage the connection lifecycle",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.WaitForConditionAsync(
                            () => client.IsConnected,
                            TimeSpan.FromSeconds(2),
                            "Client never connected.",
                            token).ConfigureAwait(false);

                        client.Disconnect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.False(client.IsConnected, "Client should be disconnected after Disconnect().");
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "BidirectionalExchange",
                    "Client and server exchange payloads in both directions",
                    async token =>
                    {
                        string? receivedByServer = null;
                        string? receivedByClient = null;

                        TaskCompletionSource<bool> serverReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        TaskCompletionSource<bool> clientReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.DataReceived += (_, args) =>
                        {
                            receivedByServer = TestEnvironment.GetString(args.Data);
                            serverReceived.TrySetResult(true);
                        };
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Events.DataReceived += (_, args) =>
                        {
                            receivedByClient = TestEnvironment.GetString(args.Data);
                            clientReceived.TrySetResult(true);
                        };
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        client.Send("Hello from client");
                        await TestEnvironment.WithTimeoutAsync(
                            serverReceived.Task,
                            TimeSpan.FromSeconds(5),
                            "Server did not receive client data.").ConfigureAwait(false);
                        TestAssert.Equal("Hello from client", receivedByServer, "Server received the wrong payload.");

                        string clientId = TestEnvironment.GetSingleClient(server);
                        server.Send(clientId, "Hello from server");
                        await TestEnvironment.WithTimeoutAsync(
                            clientReceived.Task,
                            TimeSpan.FromSeconds(5),
                            "Client did not receive server data.").ConfigureAwait(false);
                        TestAssert.Equal("Hello from server", receivedByClient, "Client received the wrong payload.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "MultipleClients",
                    "SimpleTcpServer tracks multiple connected clients",
                    async token =>
                    {
                        TaskCompletionSource<bool> allConnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        int connectedCount = 0;

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.ClientConnected += (_, _) =>
                        {
                            if (Interlocked.Increment(ref connectedCount) == 3)
                                allConnected.TrySetResult(true);
                        };
                        server.Start();

                        using SimpleTcpClient client1 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        using SimpleTcpClient client2 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        using SimpleTcpClient client3 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");

                        client1.Connect();
                        client2.Connect();
                        client3.Connect();

                        await TestEnvironment.WithTimeoutAsync(
                            allConnected.Task,
                            TimeSpan.FromSeconds(5),
                            "All clients did not connect.").ConfigureAwait(false);

                        TestAssert.Equal(3, server.Connections, "Server should report three connections.");

                        client1.Disconnect();
                        client2.Disconnect();
                        client3.Disconnect();

                        await TestEnvironment.WaitForConditionAsync(
                            () => server.Connections == 0,
                            TimeSpan.FromSeconds(5),
                            "Server did not observe all disconnects.",
                            token).ConfigureAwait(false);

                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientEnumeration",
                    "SimpleTcpServer.GetClients and IsConnected reflect active clients",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client1 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        using SimpleTcpClient client2 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");

                        client1.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client2.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        List<string> clients = server.GetClients().ToList();
                        TestAssert.Equal(2, clients.Count, "Expected two enumerated clients.");
                        foreach (string clientId in clients)
                            TestAssert.True(server.IsConnected(clientId), $"Client {clientId} should report as connected.");

                        client1.Disconnect();
                        await TestEnvironment.WaitForConditionAsync(
                            () => server.GetClients().Count() == 1,
                            TimeSpan.FromSeconds(5),
                            "Server did not update the client list after disconnect.",
                            token).ConfigureAwait(false);

                        client2.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConnectNoOpWhenConnected",
                    "SimpleTcpClient.Connect is idempotent once connected",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "Client should remain connected after a second Connect().");
                        TestAssert.Equal(1, server.Connections, "Server should still have only one connection.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ClientDisconnectNoOpWhenDisconnected",
                    "SimpleTcpClient.Disconnect is safe when already disconnected",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10200");
                        client.Disconnect();
                        TestAssert.False(client.IsConnected, "Client should remain disconnected.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "MaxConnectionsEnforced",
                    "SimpleTcpServer respects MaxConnections",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Settings.MaxConnections = 2;
                        server.Start();

                        using SimpleTcpClient client1 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        using SimpleTcpClient client2 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        using SimpleTcpClient client3 = new($"{TestEnvironment.LoopbackIp}:{server.Port}");

                        client1.Connect();
                        client2.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        try
                        {
                            client3.Connect();
                            await Task.Delay(300, token).ConfigureAwait(false);
                        }
                        catch
                        {
                        }

                        TestAssert.True(server.Connections <= 2, "Server exceeded MaxConnections.");

                        client1.Disconnect();
                        client2.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "LoggerCallbacks",
                    "Client and server logger callbacks receive messages",
                    async token =>
                    {
                        List<string> serverLogs = new();
                        List<string> clientLogs = new();
                        object sync = new();

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Logger = message =>
                        {
                            lock (sync)
                            {
                                serverLogs.Add(message);
                            }
                        };
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Logger = message =>
                        {
                            lock (sync)
                            {
                                clientLogs.Add(message);
                            }
                        };
                        client.Connect();
                        await Task.Delay(200, token).ConfigureAwait(false);

                        TestAssert.True(clientLogs.Count > 0, "Client logger should capture messages.");
                        TestAssert.True(serverLogs.Count > 0, "Server logger should capture messages.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerEndpointAndPortProperties",
                    "SimpleTcpServer exposes endpoint, port, and IP address after start",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(server.Port > 0, "Server.Port should be assigned after Start().");
                        IPEndPoint endpoint = (IPEndPoint)TestAssert.NotNull(
                            server.Endpoint as IPEndPoint,
                            "Server.Endpoint should be populated after Start().");
                        TestAssert.Equal(server.Port, endpoint.Port, "Server.Endpoint should expose the active port.");
                        TestAssert.Equal(IPAddress.Parse(TestEnvironment.LoopbackIp), server.IpAddress, "Server.IpAddress should reflect the configured listener.");

                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientLocalEndpointProperty",
                    "SimpleTcpClient.LocalEndpoint is populated after connect",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        IPEndPoint endpoint = TestAssert.NotNull(
                            client.LocalEndpoint,
                            "LocalEndpoint should be populated when connected.");
                        TestAssert.Equal(IPAddress.Parse(TestEnvironment.LoopbackIp), endpoint.Address, "LocalEndpoint should use loopback.");
                        TestAssert.True(endpoint.Port > 0, "LocalEndpoint should expose an ephemeral local port.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendToUnknownClientNoThrow",
                    "SimpleTcpServer.Send silently ignores unknown clients and still validates inputs",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        server.Send("127.0.0.1:9999", "test");
                        TestAssert.Throws<ArgumentNullException>(() => server.Send(string.Empty, "test"));
                        StopServer(server);
                    }),
            });
    }

    public static TestSuiteDescriptor EventSuite()
    {
        const string suiteId = "Events";

        return new TestSuiteDescriptor(
            suiteId,
            "Events",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Async(
                    suiteId,
                    "ServerClientConnected",
                    "SimpleTcpServer raises ClientConnected with the connected endpoint",
                    async token =>
                    {
                        TaskCompletionSource<ConnectionEventArgs> connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.ClientConnected += (_, args) => connected.TrySetResult(args);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();

                        ConnectionEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            connected.Task,
                            TimeSpan.FromSeconds(5),
                            "ClientConnected did not fire.").ConfigureAwait(false);
                        TestAssert.True(!string.IsNullOrEmpty(args.IpPort), "ClientConnected should include the client ip:port.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerClientDisconnectedNormal",
                    "SimpleTcpServer raises ClientDisconnected with reason Normal",
                    async token =>
                    {
                        TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.ClientDisconnected += (_, args) => disconnected.TrySetResult(args);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();

                        ConnectionEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            disconnected.Task,
                            TimeSpan.FromSeconds(5),
                            "ClientDisconnected did not fire.").ConfigureAwait(false);
                        TestAssert.Equal(DisconnectReason.Normal, args.Reason, "Client disconnect should surface DisconnectReason.Normal.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerDataReceived",
                    "SimpleTcpServer raises DataReceived with the transmitted payload",
                    async token =>
                    {
                        TaskCompletionSource<DataReceivedEventArgs> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.DataReceived += (_, args) => received.TrySetResult(args);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Send("Test data");

                        DataReceivedEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            received.Task,
                            TimeSpan.FromSeconds(5),
                            "DataReceived did not fire.").ConfigureAwait(false);
                        TestAssert.Equal("Test data", TestEnvironment.GetString(args.Data), "Server should receive the original payload.");
                        TestAssert.True(!string.IsNullOrEmpty(args.IpPort), "DataReceived should include the client ip:port.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerDataSent",
                    "SimpleTcpServer raises DataSent with the byte count",
                    async token =>
                    {
                        TaskCompletionSource<DataSentEventArgs> sent = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.DataSent += (_, args) => sent.TrySetResult(args);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        server.Send(TestEnvironment.GetSingleClient(server), "Test message");

                        DataSentEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            sent.Task,
                            TimeSpan.FromSeconds(5),
                            "DataSent did not fire.").ConfigureAwait(false);
                        TestAssert.Equal(12L, args.BytesSent, "DataSent should report the payload length.");
                        TestAssert.True(!string.IsNullOrEmpty(args.IpPort), "DataSent should include the client ip:port.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConnected",
                    "SimpleTcpClient raises Connected when the socket is established",
                    async token =>
                    {
                        TaskCompletionSource<ConnectionEventArgs> connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Events.Connected += (_, args) => connected.TrySetResult(args);
                        client.Connect();

                        ConnectionEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            connected.Task,
                            TimeSpan.FromSeconds(5),
                            "Connected did not fire.").ConfigureAwait(false);
                        TestAssert.Equal(client.ServerIpPort, args.IpPort, "Connected should report the remote server ip:port.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientDisconnectedNormal",
                    "SimpleTcpClient raises Disconnected with reason Normal",
                    async token =>
                    {
                        TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Events.Disconnected += (_, args) => disconnected.TrySetResult(args);
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();

                        ConnectionEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            disconnected.Task,
                            TimeSpan.FromSeconds(5),
                            "Disconnected did not fire.").ConfigureAwait(false);
                        TestAssert.Equal(DisconnectReason.Normal, args.Reason, "Disconnect() should surface DisconnectReason.Normal.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientDataReceived",
                    "SimpleTcpClient raises DataReceived with the transmitted payload",
                    async token =>
                    {
                        TaskCompletionSource<DataReceivedEventArgs> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Events.DataReceived += (_, args) => received.TrySetResult(args);
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        server.Send(TestEnvironment.GetSingleClient(server), "Hello client");

                        DataReceivedEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            received.Task,
                            TimeSpan.FromSeconds(5),
                            "Client DataReceived did not fire.").ConfigureAwait(false);
                        TestAssert.Equal("Hello client", TestEnvironment.GetString(args.Data), "Client should receive the original payload.");
                        TestAssert.Equal(client.ServerIpPort, args.IpPort, "Client DataReceived should report the server ip:port.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientDataSent",
                    "SimpleTcpClient raises DataSent with the byte count",
                    async token =>
                    {
                        TaskCompletionSource<DataSentEventArgs> sent = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Events.DataSent += (_, args) => sent.TrySetResult(args);
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Send("Hello");

                        DataSentEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            sent.Task,
                            TimeSpan.FromSeconds(5),
                            "Client DataSent did not fire.").ConfigureAwait(false);
                        TestAssert.Equal(5L, args.BytesSent, "DataSent should report the payload length.");
                        TestAssert.Equal(client.ServerIpPort, args.IpPort, "Client DataSent should report the server ip:port.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerInitiatedDisconnectIsKicked",
                    "SimpleTcpServer.DisconnectClient raises ClientDisconnected with reason Kicked",
                    async token =>
                    {
                        TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.ClientDisconnected += (_, args) =>
                        {
                            if (args.Reason == DisconnectReason.Kicked)
                                disconnected.TrySetResult(args);
                        };
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        server.DisconnectClient(TestEnvironment.GetSingleClient(server));

                        ConnectionEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            disconnected.Task,
                            TimeSpan.FromSeconds(5),
                            "Server did not surface a kicked disconnect.").ConfigureAwait(false);
                        TestAssert.Equal(DisconnectReason.Kicked, args.Reason, "DisconnectClient should raise DisconnectReason.Kicked.");

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);
                        StopServer(server);
                    }),
            });
    }

    public static TestSuiteDescriptor AsyncConnectivitySuite()
    {
        const string suiteId = "AsyncConnectivity";

        return new TestSuiteDescriptor(
            suiteId,
            "Async Connectivity",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncBasic",
                    "SimpleTcpClient.ConnectAsync connects to a live server",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        await client.ConnectAsync(token).ConfigureAwait(false);
                        TestAssert.True(client.IsConnected, "Client should connect through ConnectAsync.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncPreCanceled",
                    "SimpleTcpClient.ConnectAsync honors pre-canceled tokens",
                    async _ =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        using CancellationTokenSource cts = new();
                        cts.Cancel();

                        await TestAssert.ThrowsAsync<OperationCanceledException>(
                            () => client.ConnectAsync(cts.Token)).ConfigureAwait(false);
                        TestAssert.False(client.IsConnected, "Client should remain disconnected when ConnectAsync is canceled.");

                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncAlreadyConnected",
                    "SimpleTcpClient.ConnectAsync is idempotent once connected",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        await client.ConnectAsync(token).ConfigureAwait(false);
                        await client.ConnectAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "Client should remain connected after a second ConnectAsync call.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "DisconnectAsync",
                    "SimpleTcpClient.DisconnectAsync closes an active connection",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        await client.ConnectAsync(token).ConfigureAwait(false);
                        await client.DisconnectAsync().ConfigureAwait(false);

                        TestAssert.False(client.IsConnected, "DisconnectAsync should leave the client disconnected.");
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerStartAsync",
                    "SimpleTcpServer.StartAsync begins listening",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        Task startTask = server.StartAsync();
                        await TestEnvironment.WaitForConditionAsync(
                            () => server.IsListening,
                            TimeSpan.FromSeconds(2),
                            "Server did not start listening via StartAsync.",
                            token).ConfigureAwait(false);

                        StopServer(server);
                        await ObserveServerTaskAsync(startTask).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectWithRetriesSucceeds",
                    "SimpleTcpClient.ConnectWithRetries succeeds when a server appears within the timeout window",
                    async token =>
                    {
                        int port = TestEnvironment.GetFreeTcpPort();
                        using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{port}");
                        Task serverTask = Task.Run(
                            async () =>
                            {
                                await Task.Delay(250, delayCts.Token).ConfigureAwait(false);
                                using SimpleTcpServer delayedServer = new(TestEnvironment.LoopbackIp, port);
                                delayedServer.Start();
                                await Task.Delay(750, delayCts.Token).ConfigureAwait(false);
                                StopServer(delayedServer);
                            },
                            delayCts.Token);

                        client.ConnectWithRetries(2000);
                        TestAssert.True(client.IsConnected, "ConnectWithRetries should eventually connect once the server appears.");

                        client.Disconnect();
                        delayCts.Cancel();
                        try
                        {
                            await serverTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ConnectWithRetriesInvalidTimeout",
                    "SimpleTcpClient.ConnectWithRetries validates timeout arguments",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10201");
                        TestAssert.Throws<ArgumentException>(() => client.ConnectWithRetries(0));
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncFaultPropagatesSocketException",
                    "SimpleTcpClient.ConnectAsync propagates socket failures",
                    async _ =>
                    {
                        int port = TestEnvironment.GetFreeTcpPort();
                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{port}");
                        client.Settings.ConnectTimeoutMs = 5000;

                        await TestAssert.ThrowsAsync<SocketException>(
                            () => client.ConnectAsync()).ConfigureAwait(false);
                        TestAssert.False(client.IsConnected, "Client should remain disconnected after a socket failure.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncConcurrentCallsUseOnePhysicalConnection",
                    "Concurrent ConnectAsync calls result in a single physical connection",
                    async _ =>
                    {
                        int serverConnectedCount = 0;

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Events.ClientConnected += (_, _) => Interlocked.Increment(ref serverConnectedCount);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        Task[] tasks = Enumerable.Range(0, 25)
                            .Select(_ => Task.Run(() => client.ConnectAsync()))
                            .ToArray();

                        await TestEnvironment.WithTimeoutAsync(
                            Task.WhenAll(tasks),
                            TimeSpan.FromSeconds(5),
                            "Concurrent ConnectAsync calls did not complete in time.").ConfigureAwait(false);

                        await Task.Delay(50).ConfigureAwait(false);
                        TestAssert.Equal(1, serverConnectedCount, "Server should see exactly one physical connection.");
                        TestAssert.True(client.IsConnected, "Client should end in the connected state.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncTimeoutReleasesMutex",
                    "ConnectAsync timeout does not deadlock subsequent attempts",
                    async _ =>
                    {
                        using TestEnvironment.BacklogBlackhole blackhole =
                            await TestEnvironment.RequireBacklogBlackholeAsync().ConfigureAwait(false);

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{blackhole.Port}");
                        client.Settings.ConnectTimeoutMs = 150;

                        await TestAssert.ThrowsAsync<TimeoutException>(
                            () => TestEnvironment.WithTimeoutAsync(
                                client.ConnectAsync(),
                                TimeSpan.FromSeconds(2),
                                "Initial ConnectAsync did not complete in time.")).ConfigureAwait(false);

                        await TestAssert.ThrowsAsync<TimeoutException>(
                            () => TestEnvironment.WithTimeoutAsync(
                                client.ConnectAsync(),
                                TimeSpan.FromSeconds(2),
                                "Second ConnectAsync did not complete in time.")).ConfigureAwait(false);

                        TestAssert.False(client.IsConnected, "Client should remain disconnected after repeated timeouts.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncSecondAttemptCanSucceed",
                    "ConnectAsync can succeed after a previous timeout once the connection becomes available",
                    async _ =>
                    {
                        using TestEnvironment.BacklogBlackhole blackhole =
                            await TestEnvironment.RequireBacklogBlackholeAsync().ConfigureAwait(false);

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{blackhole.Port}");
                        client.Settings.ConnectTimeoutMs = 150;

                        await TestAssert.ThrowsAsync<TimeoutException>(
                            () => TestEnvironment.WithTimeoutAsync(
                                client.ConnectAsync(),
                                TimeSpan.FromSeconds(2),
                                "Initial ConnectAsync did not time out as expected.")).ConfigureAwait(false);

                        await blackhole.AcceptOneAsync().ConfigureAwait(false);
                        await TestEnvironment.WithTimeoutAsync(
                            client.ConnectAsync(),
                            TimeSpan.FromSeconds(2),
                            "Second ConnectAsync did not succeed in time.").ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "Client should connect on the second attempt once the backlog clears.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncCancellationWhileWaitingForMutex",
                    "ConnectAsync can be canceled while waiting for the internal connect mutex",
                    async _ =>
                    {
                        using TestEnvironment.BacklogBlackhole blackhole =
                            await TestEnvironment.RequireBacklogBlackholeAsync().ConfigureAwait(false);

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{blackhole.Port}");
                        client.Settings.ConnectTimeoutMs = 5000;

                        using CancellationTokenSource ctsA = new();
                        ctsA.CancelAfter(2000);
                        Task taskA = Task.Run(() => client.ConnectAsync(ctsA.Token));

                        await Task.Delay(25).ConfigureAwait(false);

                        using CancellationTokenSource ctsB = new();
                        ctsB.CancelAfter(50);
                        Task taskB = Task.Run(() => client.ConnectAsync(ctsB.Token));

                        await TestAssert.ThrowsAsync<OperationCanceledException>(
                            () => TestEnvironment.WithTimeoutAsync(
                                taskB,
                                TimeSpan.FromSeconds(2),
                                "Canceled ConnectAsync did not complete in time.")).ConfigureAwait(false);

                        await blackhole.AcceptOneAsync().ConfigureAwait(false);
                        await taskA.ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "First ConnectAsync call should still succeed.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncCancellationBeatsTimeout",
                    "ConnectAsync reports cancellation when cancellation wins the race against timeout",
                    async _ =>
                    {
                        using TestEnvironment.BacklogBlackhole blackhole =
                            await TestEnvironment.RequireBacklogBlackholeAsync().ConfigureAwait(false);

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{blackhole.Port}");
                        client.Settings.ConnectTimeoutMs = 200;

                        using CancellationTokenSource cts = new();
                        cts.CancelAfter(25);

                        await TestAssert.ThrowsAsync<OperationCanceledException>(
                            () => TestEnvironment.WithTimeoutAsync(
                                client.ConnectAsync(cts.Token),
                                TimeSpan.FromSeconds(2),
                                "Canceled ConnectAsync did not complete in time.")).ConfigureAwait(false);

                        TestAssert.False(client.IsConnected, "Client should remain disconnected when cancellation wins.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncNoUnobservedTaskExceptionAfterAbort",
                    "ConnectAsync timeout and cancellation paths do not leak unobserved task exceptions",
                    async _ =>
                    {
                        using TestEnvironment.BacklogBlackhole blackhole =
                            await TestEnvironment.RequireBacklogBlackholeAsync().ConfigureAwait(false);

                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        ConcurrentQueue<AggregateException> unobserved = new();
                        void Handler(object? sender, UnobservedTaskExceptionEventArgs args)
                        {
                            unobserved.Enqueue(args.Exception);
                            args.SetObserved();
                        }

                        TaskScheduler.UnobservedTaskException += Handler;
                        try
                        {
                            using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{blackhole.Port}");
                            client.Settings.ConnectTimeoutMs = 150;

                            await TestAssert.ThrowsAsync<TimeoutException>(
                                () => TestEnvironment.WithTimeoutAsync(
                                    client.ConnectAsync(),
                                    TimeSpan.FromSeconds(2),
                                    "Timed out ConnectAsync did not complete in time.")).ConfigureAwait(false);

                            using CancellationTokenSource cts = new();
                            cts.CancelAfter(75);
                            client.Settings.ConnectTimeoutMs = 5000;

                            await TestAssert.ThrowsAsync<OperationCanceledException>(
                                () => TestEnvironment.WithTimeoutAsync(
                                    client.ConnectAsync(cts.Token),
                                    TimeSpan.FromSeconds(2),
                                    "Canceled ConnectAsync did not complete in time.")).ConfigureAwait(false);

                            await Task.Delay(50).ConfigureAwait(false);

                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();

                            TestAssert.Equal(0, unobserved.Count, "ConnectAsync should not leave unobserved task exceptions behind.");
                        }
                        finally
                        {
                            TaskScheduler.UnobservedTaskException -= Handler;
                        }
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ConnectAsyncCancellationAfterSuccessDoesNotDisconnect",
                    "Canceling an external ConnectAsync token after connection does not tear down the socket",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        using CancellationTokenSource cts = new();
                        await client.ConnectAsync(cts.Token).ConfigureAwait(false);
                        TestAssert.True(client.IsConnected, "Client should connect successfully.");

                        cts.Cancel();
                        await Task.Delay(25, token).ConfigureAwait(false);
                        TestAssert.True(client.IsConnected, "Client should remain connected after external token cancellation.");

                        client.Disconnect();
                        StopServer(server);
                    }),
            });
    }

    public static TestSuiteDescriptor SslSuite()
    {
        const string suiteId = "Ssl";

        return new TestSuiteDescriptor(
            suiteId,
            "SSL",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Async(
                    suiteId,
                    "ServerByteArrayConstructor",
                    "SimpleTcpServer(string,int,byte[]) accepts SSL clients",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using X509Certificate2 clientCert = TestEnvironment.LoadCertificate(certificateBytes);
                        using SimpleTcpClient client = new(IPAddress.Parse(TestEnvironment.LoopbackIp), server.Port, clientCert);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "SSL client should connect through the byte-array server constructor.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientByteArrayConstructor",
                    "SimpleTcpClient(string,int,byte[]) connects to SSL servers",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using SimpleTcpClient client = new(TestEnvironment.LoopbackIp, server.Port, certificateBytes);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "SSL client should connect through the byte-array client constructor.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "BidirectionalExchange",
                    "SSL client and server exchange payloads in both directions",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        string? receivedByServer = null;
                        string? receivedByClient = null;
                        TaskCompletionSource<bool> serverReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        TaskCompletionSource<bool> clientReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Events.DataReceived += (_, args) =>
                        {
                            receivedByServer = TestEnvironment.GetString(args.Data);
                            serverReceived.TrySetResult(true);
                        };
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using SimpleTcpClient client = new(IPAddress.Parse(TestEnvironment.LoopbackIp), server.Port, certificateBytes);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Events.DataReceived += (_, args) =>
                        {
                            receivedByClient = TestEnvironment.GetString(args.Data);
                            clientReceived.TrySetResult(true);
                        };
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        client.Send("Hello SSL");
                        await TestEnvironment.WithTimeoutAsync(
                            serverReceived.Task,
                            TimeSpan.FromSeconds(5),
                            "SSL server did not receive data.").ConfigureAwait(false);
                        TestAssert.Equal("Hello SSL", receivedByServer, "SSL server received the wrong payload.");

                        server.Send(TestEnvironment.GetSingleClient(server), "Hello client");
                        await TestEnvironment.WithTimeoutAsync(
                            clientReceived.Task,
                            TimeSpan.FromSeconds(5),
                            "SSL client did not receive data.").ConfigureAwait(false);
                        TestAssert.Equal("Hello client", receivedByClient, "SSL client received the wrong payload.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "PfxFilePathConstructors",
                    "File-based SSL constructors work on both client and server",
                    async token =>
                    {
                        using SimpleTcpServer server = new(
                            $"{TestEnvironment.LoopbackIp}:0",
                            true,
                            TestEnvironment.CertificatePath,
                            "simpletcp");
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using SimpleTcpClient client = new(
                            $"{TestEnvironment.LoopbackIp}:{server.Port}",
                            true,
                            TestEnvironment.CertificatePath,
                            "simpletcp");
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "File-based SSL constructors should connect successfully.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientValidationCallback",
                    "SimpleTcpClientSettings.CertificateValidationCallback is used during SSL handshake",
                    async token =>
                    {
                        bool callbackInvoked = false;
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using SimpleTcpClient client = new(new IPEndPoint(IPAddress.Parse(TestEnvironment.LoopbackIp), server.Port), certificateBytes);
                        client.Settings.AcceptInvalidCertificates = false;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Settings.CertificateValidationCallback = (_, _, _, _) =>
                        {
                            callbackInvoked = true;
                            return true;
                        };
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "Client should connect with a custom validation callback.");
                        TestAssert.True(callbackInvoked, "Client certificate validation callback should be invoked.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerValidationCallback",
                    "SimpleTcpServerSettings.CertificateValidationCallback is used when mutual TLS is enabled",
                    async token =>
                    {
                        bool callbackInvoked = false;
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = false;
                        server.Settings.MutuallyAuthenticate = true;
                        server.Settings.CertificateValidationCallback = (_, _, _, _) =>
                        {
                            callbackInvoked = true;
                            return true;
                        };
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using X509Certificate2 clientCertificate = TestEnvironment.LoadCertificate(certificateBytes);
                        using SimpleTcpClient client = new(TestEnvironment.LoopbackIp, server.Port, clientCertificate);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "Mutual TLS client should connect with a custom server validation callback.");
                        TestAssert.True(callbackInvoked, "Server certificate validation callback should be invoked.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConstructorIpAddressPortSslPfx",
                    "SimpleTcpClient(IPAddress,int,bool,string,string) connects to SSL servers",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using SimpleTcpClient client = new(
                            IPAddress.Parse(TestEnvironment.LoopbackIp),
                            server.Port,
                            true,
                            TestEnvironment.CertificatePath,
                            "simpletcp");
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "IPAddress+Port+SSL PFX constructor should connect.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConstructorEndpointSslPfx",
                    "SimpleTcpClient(IPEndPoint,bool,string,string) connects to SSL servers",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using SimpleTcpClient client = new(
                            new IPEndPoint(IPAddress.Parse(TestEnvironment.LoopbackIp), server.Port),
                            true,
                            TestEnvironment.CertificatePath,
                            "simpletcp");
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "IPEndPoint+SSL PFX constructor should connect.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConstructorStringPortX509",
                    "SimpleTcpClient(string,int,X509Certificate2) connects to SSL servers",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using X509Certificate2 clientCertificate = TestEnvironment.LoadCertificate(certificateBytes);
                        using SimpleTcpClient client = new(TestEnvironment.LoopbackIp, server.Port, clientCertificate);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "String+Port+X509 constructor should connect.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConstructorIpAddressPortX509",
                    "SimpleTcpClient(IPAddress,int,X509Certificate2) connects to SSL servers",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using X509Certificate2 clientCertificate = TestEnvironment.LoadCertificate(certificateBytes);
                        using SimpleTcpClient client = new(IPAddress.Parse(TestEnvironment.LoopbackIp), server.Port, clientCertificate);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "IPAddress+Port+X509 constructor should connect.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientConstructorEndpointX509",
                    "SimpleTcpClient(IPEndPoint,X509Certificate2) connects to SSL servers",
                    async token =>
                    {
                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0, certificateBytes);
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        using X509Certificate2 clientCertificate = TestEnvironment.LoadCertificate(certificateBytes);
                        using SimpleTcpClient client = new(
                            new IPEndPoint(IPAddress.Parse(TestEnvironment.LoopbackIp), server.Port),
                            clientCertificate);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "IPEndPoint+X509 constructor should connect.");
                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerConstructorIpPortSslPfx",
                    "SimpleTcpServer(string,int,bool,string,string) accepts SSL clients",
                    async token =>
                    {
                        using SimpleTcpServer server = new(
                            TestEnvironment.LoopbackIp,
                            0,
                            true,
                            TestEnvironment.CertificatePath,
                            "simpletcp");
                        server.Settings.AcceptInvalidCertificates = true;
                        server.Start();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        byte[] certificateBytes = TestEnvironment.ExportCertificateBytes();
                        using SimpleTcpClient client = new(new IPEndPoint(IPAddress.Parse(TestEnvironment.LoopbackIp), server.Port), certificateBytes);
                        client.Settings.AcceptInvalidCertificates = true;
                        client.Settings.MutuallyAuthenticate = false;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "IP+Port+SSL PFX server constructor should accept SSL clients.");
                        client.Disconnect();
                        StopServer(server);
                    }),
            });
    }

    public static TestSuiteDescriptor TimeoutAndBehaviorSuite()
    {
        const string suiteId = "TimeoutsAndBehavior";

        return new TestSuiteDescriptor(
            suiteId,
            "Timeouts And Behavior",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Async(
                    suiteId,
                    "IdleClientTimeout",
                    "SimpleTcpServer disconnects idle clients with reason Timeout",
                    async _ =>
                    {
                        TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Settings.IdleClientTimeoutMs = 2000;
                        server.Settings.IdleClientEvaluationIntervalMs = 500;
                        server.Events.ClientDisconnected += (_, args) =>
                        {
                            if (args.Reason == DisconnectReason.Timeout)
                                disconnected.TrySetResult(args);
                        };
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();

                        ConnectionEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            disconnected.Task,
                            TimeSpan.FromSeconds(10),
                            "Idle client timeout did not disconnect the client.").ConfigureAwait(false);
                        TestAssert.Equal(DisconnectReason.Timeout, args.Reason, "Idle client timeout should surface DisconnectReason.Timeout.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "IdleServerTimeout",
                    "SimpleTcpClient disconnects from idle servers with reason Timeout",
                    async _ =>
                    {
                        TaskCompletionSource<ConnectionEventArgs> disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Settings.IdleServerTimeoutMs = 2000;
                        client.Settings.IdleServerEvaluationIntervalMs = 500;
                        client.Events.Disconnected += (_, args) =>
                        {
                            if (args.Reason == DisconnectReason.Timeout)
                                disconnected.TrySetResult(args);
                        };
                        client.Connect();

                        ConnectionEventArgs args = await TestEnvironment.WithTimeoutAsync(
                            disconnected.Task,
                            TimeSpan.FromSeconds(10),
                            "Idle server timeout did not disconnect the client.").ConfigureAwait(false);
                        TestAssert.Equal(DisconnectReason.Timeout, args.Reason, "Idle server timeout should surface DisconnectReason.Timeout.");

                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "PermittedIpAllowsLoopback",
                    "SimpleTcpServerSettings.PermittedIPs allows configured loopback clients",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Settings.PermittedIPs.Add(TestEnvironment.LoopbackIp);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.WaitForConditionAsync(
                            () => server.Connections == 1,
                            TimeSpan.FromSeconds(5),
                            "Permitted loopback client did not connect.",
                            token).ConfigureAwait(false);

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "BlockedIpRejectsLoopback",
                    "SimpleTcpServerSettings.BlockedIPs rejects configured loopback clients",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Settings.BlockedIPs.Add(TestEnvironment.LoopbackIp);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await Task.Delay(500, token).ConfigureAwait(false);

                        TestAssert.Equal(0, server.Connections, "Blocked loopback client should never become a tracked connection.");
                        await TestEnvironment.WaitForConditionAsync(
                            () => !client.IsConnected,
                            TimeSpan.FromSeconds(5),
                            "Blocked client did not transition back to disconnected.",
                            token).ConfigureAwait(false);

                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerAsyncDataEventsEnabled",
                    "SimpleTcpServerSettings.UseAsyncDataReceivedEvents can dispatch work on multiple threads",
                    async token =>
                    {
                        ConcurrencyTracker tracker = new(expectedEvents: 100);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Settings.UseAsyncDataReceivedEvents = true;
                        server.Events.DataReceived += (_, _) => tracker.Record();
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();

                        for (int i = 0; i < 100; i++)
                        {
                            await client.SendAsync($"Message {i}", token).ConfigureAwait(false);
                            await Task.Delay(10, token).ConfigureAwait(false);
                        }

                        await tracker.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                        await Task.Delay(500, token).ConfigureAwait(false);
                        TestAssert.True(
                            tracker.CallingThreadIds.Distinct().Count() > 1,
                            "Async server DataReceived handlers should execute on more than one thread over repeated deliveries.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ServerAsyncDataEventsDisabled",
                    "SimpleTcpServerSettings.UseAsyncDataReceivedEvents=false prevents concurrent event execution",
                    async token =>
                    {
                        ConcurrencyTracker tracker = new(expectedEvents: 100);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Settings.UseAsyncDataReceivedEvents = false;
                        server.Events.DataReceived += (_, _) => tracker.Record();
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();

                        for (int i = 0; i < 100; i++)
                        {
                            await client.SendAsync($"Message {i}", token).ConfigureAwait(false);
                            await Task.Delay(10, token).ConfigureAwait(false);
                        }

                        await tracker.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                        await Task.Delay(250, token).ConfigureAwait(false);
                        TestAssert.False(
                            tracker.ConcurrencyDetected,
                            "Synchronous server DataReceived handlers should not overlap.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "ClientAsyncDataEventsDisabled",
                    "SimpleTcpClientSettings.UseAsyncDataReceivedEvents=false prevents concurrent client event execution",
                    async token =>
                    {
                        ConcurrencyTracker tracker = new(expectedEvents: 100);

                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Settings.UseAsyncDataReceivedEvents = false;
                        client.Events.DataReceived += (_, _) => tracker.Record();
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        string clientId = TestEnvironment.GetSingleClient(server);
                        for (int i = 0; i < 100; i++)
                        {
                            await server.SendAsync(clientId, $"Message {i}", token).ConfigureAwait(false);
                            await Task.Delay(10, token).ConfigureAwait(false);
                        }

                        await tracker.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                        await Task.Delay(250, token).ConfigureAwait(false);
                        TestAssert.False(
                            tracker.ConcurrencyDetected,
                            "Synchronous client DataReceived handlers should not overlap.");

                        client.Disconnect();
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "KeepalivesEnabledRoundTrip",
                    "Client and server remain connected when TCP keepalives are enabled",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        server.Keepalive.EnableTcpKeepAlives = true;
                        server.Keepalive.TcpKeepAliveInterval = 5;
                        server.Keepalive.TcpKeepAliveTime = 5;
                        server.Keepalive.TcpKeepAliveRetryCount = 3;
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Keepalive.EnableTcpKeepAlives = true;
                        client.Keepalive.TcpKeepAliveInterval = 5;
                        client.Keepalive.TcpKeepAliveTime = 5;
                        client.Keepalive.TcpKeepAliveRetryCount = 3;
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.IsConnected, "Client should connect with keepalives enabled.");
                        client.Disconnect();
                        StopServer(server);
                    }),
            });
    }

    private sealed class ConcurrencyTracker
    {
        private readonly TaskCompletionSource<bool> _completed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<int> _callingThreadIds = new();
        private readonly int _expectedEvents;
        private int _activeCount;
        private int _receivedCount;

        public ConcurrencyTracker(int expectedEvents)
        {
            _expectedEvents = expectedEvents;
        }

        public IReadOnlyList<int> CallingThreadIds => _callingThreadIds;

        public bool ConcurrencyDetected { get; private set; }

        public void Record()
        {
            if (Interlocked.Increment(ref _activeCount) > 1)
                ConcurrencyDetected = true;

            lock (_callingThreadIds)
            {
                _callingThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
            }

            if (Interlocked.Increment(ref _receivedCount) >= _expectedEvents)
                _completed.TrySetResult(true);

            Interlocked.Decrement(ref _activeCount);
        }

        public Task WaitAsync(TimeSpan timeout)
        {
            return TestEnvironment.WithTimeoutAsync(
                _completed.Task,
                timeout,
                "Expected event count was not observed.");
        }
    }

    private static async Task ObserveServerTaskAsync(Task task)
    {
        await Task.WhenAny(task, Task.Delay(2000)).ConfigureAwait(false);

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
