namespace Test.Shared.Suites;

using System.IO;

using SuperSimpleTcp;

using Touchstone.Core;

internal static partial class RuntimeSuites
{
    public static TestSuiteDescriptor ClientSendValidationSuite()
    {
        const string suiteId = "ClientSendValidation";

        return new TestSuiteDescriptor(
            suiteId,
            "Client Send Validation",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "SendStringNullThrows",
                    "SimpleTcpClient.Send(string) rejects null",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10100");
                        TestAssert.Throws<ArgumentNullException>(() => client.Send((string)null!));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStringEmptyThrows",
                    "SimpleTcpClient.Send(string) rejects empty",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10101");
                        TestAssert.Throws<ArgumentNullException>(() => client.Send(string.Empty));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStringNotConnectedThrows",
                    "SimpleTcpClient.Send(string) rejects disconnected clients",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10102");
                        TestAssert.Throws<IOException>(() => client.Send("hello"));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendBytesNullThrows",
                    "SimpleTcpClient.Send(byte[]) rejects null",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10103");
                        TestAssert.Throws<ArgumentNullException>(() => client.Send((byte[])null!));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendBytesEmptyThrows",
                    "SimpleTcpClient.Send(byte[]) rejects empty payloads",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10104");
                        TestAssert.Throws<ArgumentNullException>(() => client.Send(Array.Empty<byte>()));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendBytesNotConnectedThrows",
                    "SimpleTcpClient.Send(byte[]) rejects disconnected clients",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10105");
                        TestAssert.Throws<IOException>(() => client.Send(new byte[] { 1 }));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamZeroLengthNoThrow",
                    "SimpleTcpClient.Send(long,Stream) ignores zero length payloads",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10106");
                        client.Send(0, Stream.Null);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamNullThrows",
                    "SimpleTcpClient.Send(long,Stream) rejects null streams",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10107");
                        TestAssert.Throws<ArgumentNullException>(() => client.Send(10, (Stream)null!));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamUnreadableThrows",
                    "SimpleTcpClient.Send(long,Stream) rejects unreadable streams",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10108");
                        using Stream stream = TestEnvironment.CreateUnreadableStream();
                        TestAssert.Throws<InvalidOperationException>(() => client.Send(10, stream));
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamNotConnectedThrows",
                    "SimpleTcpClient.Send(long,Stream) rejects disconnected clients",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10109");
                        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
                        TestAssert.Throws<IOException>(() => client.Send(stream.Length, stream));
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStringNullThrows",
                    "SimpleTcpClient.SendAsync(string) rejects null",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10110");
                        await TestAssert.ThrowsAsync<ArgumentNullException>(
                            () => client.SendAsync((string)null!)).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStringNotConnectedThrows",
                    "SimpleTcpClient.SendAsync(string) rejects disconnected clients",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10111");
                        await TestAssert.ThrowsAsync<IOException>(
                            () => client.SendAsync("hello")).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncBytesNullThrows",
                    "SimpleTcpClient.SendAsync(byte[]) rejects null",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10112");
                        await TestAssert.ThrowsAsync<ArgumentNullException>(
                            () => client.SendAsync((byte[])null!)).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncBytesEmptyThrows",
                    "SimpleTcpClient.SendAsync(byte[]) rejects empty payloads",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10113");
                        await TestAssert.ThrowsAsync<ArgumentNullException>(
                            () => client.SendAsync(Array.Empty<byte>())).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncBytesNotConnectedThrows",
                    "SimpleTcpClient.SendAsync(byte[]) rejects disconnected clients",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10114");
                        await TestAssert.ThrowsAsync<IOException>(
                            () => client.SendAsync(new byte[] { 1 })).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStreamZeroLengthNoThrow",
                    "SimpleTcpClient.SendAsync(long,Stream) ignores zero length payloads",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10115");
                        await client.SendAsync(0, Stream.Null).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStreamNullThrows",
                    "SimpleTcpClient.SendAsync(long,Stream) rejects null streams",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10116");
                        await TestAssert.ThrowsAsync<ArgumentNullException>(
                            () => client.SendAsync(10, (Stream)null!)).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStreamUnreadableThrows",
                    "SimpleTcpClient.SendAsync(long,Stream) rejects unreadable streams",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10117");
                        using Stream stream = TestEnvironment.CreateUnreadableStream();
                        await TestAssert.ThrowsAsync<InvalidOperationException>(
                            () => client.SendAsync(10, stream)).ConfigureAwait(false);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStreamNotConnectedThrows",
                    "SimpleTcpClient.SendAsync(long,Stream) rejects disconnected clients",
                    async _ =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:10118");
                        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
                        await TestAssert.ThrowsAsync<IOException>(
                            () => client.SendAsync(stream.Length, stream)).ConfigureAwait(false);
                    }),
            });
    }

    public static TestSuiteDescriptor ServerSendValidationSuite()
    {
        const string suiteId = "ServerSendValidation";

        return new TestSuiteDescriptor(
            suiteId,
            "Server Send Validation",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "GetClientsEmpty",
                    "SimpleTcpServer.GetClients returns an empty list before any connections",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Empty(server.GetClients(), "GetClients should be empty before any clients connect.");
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "IsConnectedNullThrows",
                    "SimpleTcpServer.IsConnected rejects null ip:port",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.IsConnected(null!));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStringNullIpPortThrows",
                    "SimpleTcpServer.Send(string,string) rejects null ip:port",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.Send(null!, "data"));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStringNullDataThrows",
                    "SimpleTcpServer.Send(string,string) rejects null payload",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.Send("127.0.0.1:9999", (string)null!));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendBytesNullIpPortThrows",
                    "SimpleTcpServer.Send(string,byte[]) rejects null ip:port",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.Send(null!, (byte[])null!));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendBytesNullDataThrows",
                    "SimpleTcpServer.Send(string,byte[]) rejects null payload",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.Send("127.0.0.1:9999", (byte[])null!));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamNullIpPortThrows",
                    "SimpleTcpServer.Send(string,long,Stream) rejects null ip:port",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.Send(null!, 10, Stream.Null));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamZeroLengthNoThrow",
                    "SimpleTcpServer.Send(string,long,Stream) ignores zero length payloads",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        server.Send("127.0.0.1:9999", 0, Stream.Null);
                        Task.Delay(50).Wait();
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamNullThrows",
                    "SimpleTcpServer.Send(string,long,Stream) rejects null streams",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.Send("127.0.0.1:9999", 10, (Stream)null!));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SendStreamUnreadableThrows",
                    "SimpleTcpServer.Send(string,long,Stream) rejects unreadable streams",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        using Stream stream = TestEnvironment.CreateUnreadableStream();
                        TestAssert.Throws<InvalidOperationException>(() => server.Send("127.0.0.1:9999", 10, stream));
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStringNullIpPortThrows",
                    "SimpleTcpServer.SendAsync(string,string) rejects null ip:port",
                    async _ =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        await TestAssert.ThrowsAsync<ArgumentNullException>(
                            () => server.SendAsync(null!, "data")).ConfigureAwait(false);
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncBytesNullIpPortThrows",
                    "SimpleTcpServer.SendAsync(string,byte[]) rejects null ip:port",
                    async _ =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        await TestAssert.ThrowsAsync<ArgumentNullException>(
                            () => server.SendAsync(null!, new byte[] { 1 })).ConfigureAwait(false);
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStreamNullIpPortThrows",
                    "SimpleTcpServer.SendAsync(string,long,Stream) rejects null ip:port",
                    async _ =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        await TestAssert.ThrowsAsync<ArgumentNullException>(
                            () => server.SendAsync(null!, 10, Stream.Null)).ConfigureAwait(false);
                        StopServer(server);
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "SendAsyncStreamUnreadableThrows",
                    "SimpleTcpServer.SendAsync(string,long,Stream) rejects unreadable streams",
                    async _ =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        using Stream stream = TestEnvironment.CreateUnreadableStream();
                        await TestAssert.ThrowsAsync<InvalidOperationException>(
                            () => server.SendAsync("127.0.0.1:9999", 10, stream)).ConfigureAwait(false);
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "DisconnectClientNullThrows",
                    "SimpleTcpServer.DisconnectClient rejects null ip:port",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        TestAssert.Throws<ArgumentNullException>(() => server.DisconnectClient(null!));
                        StopServer(server);
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "DisconnectUnknownClientNoThrow",
                    "SimpleTcpServer.DisconnectClient is a no-op for unknown clients",
                    () =>
                    {
                        using SimpleTcpServer server = CreateStartedServer();
                        server.DisconnectClient("127.0.0.1:65000");
                        Task.Delay(50).Wait();
                        StopServer(server);
                    }),
            });
    }

    private static SimpleTcpServer CreateStartedServer()
    {
        SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
        server.Start();
        return server;
    }

    private static void StopServer(SimpleTcpServer server)
    {
        try
        {
            server.Stop();
        }
        catch (AggregateException ex) when (
            ex.InnerExceptions.All(
                inner => inner is TaskCanceledException
                    || inner is OperationCanceledException
                    || inner is ObjectDisposedException
                    || inner is InvalidOperationException))
        {
        }
    }
}
