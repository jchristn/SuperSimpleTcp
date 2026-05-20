namespace Test.Shared.Suites;

using System.Net;
using System.Security.Cryptography.X509Certificates;

using SuperSimpleTcp;

using Touchstone.Core;

internal static class ConstructorSuites
{
    public static TestSuiteDescriptor ClientConstructorSuite()
    {
        const string suiteId = "ClientConstructors";

        return new TestSuiteDescriptor(
            suiteId,
            "Client Constructors",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortNullThrows",
                    "SimpleTcpClient(string) rejects null",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient((string)null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortEmptyThrows",
                    "SimpleTcpClient(string) rejects empty",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient(string.Empty))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortNegativePortThrows",
                    "SimpleTcpClient(string) rejects negative port",
                    () => TestAssert.Throws<ArgumentException>(
                        () => new SimpleTcpClient("127.0.0.1:-1"))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortValid",
                    "SimpleTcpClient(string) stores the server endpoint",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:5000");
                        TestAssert.False(client.IsConnected, "Client should start disconnected.");
                        TestAssert.Equal("127.0.0.1:5000", client.ServerIpPort, "ServerIpPort should match constructor input.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "HostNullThrows",
                    "SimpleTcpClient(string,int) rejects null hostname",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient((string)null!, 5000))),

                TestCaseFactory.Sync(
                    suiteId,
                    "HostEmptyThrows",
                    "SimpleTcpClient(string,int) rejects empty hostname",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient(string.Empty, 5000))),

                TestCaseFactory.Sync(
                    suiteId,
                    "HostNegativePortThrows",
                    "SimpleTcpClient(string,int) rejects negative port",
                    () => TestAssert.Throws<ArgumentException>(
                        () => new SimpleTcpClient("127.0.0.1", -1))),

                TestCaseFactory.Sync(
                    suiteId,
                    "HostPortValid",
                    "SimpleTcpClient(string,int) stores host and port",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1", 5001);
                        TestAssert.False(client.IsConnected, "Client should start disconnected.");
                        TestAssert.Equal("127.0.0.1:5001", client.ServerIpPort, "ServerIpPort should be normalized.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "EndpointNullThrows",
                    "SimpleTcpClient(IPEndPoint) rejects null",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient((IPEndPoint)null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "EndpointValid",
                    "SimpleTcpClient(IPEndPoint) stores endpoint details",
                    () =>
                    {
                        using SimpleTcpClient client = new(new IPEndPoint(IPAddress.Loopback, 5002));
                        TestAssert.False(client.IsConnected, "Client should start disconnected.");
                        TestAssert.Equal("127.0.0.1:5002", client.ServerIpPort, "ServerIpPort should reflect the endpoint.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpAddressNullThrows",
                    "SimpleTcpClient(IPAddress,int) rejects null address",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient((IPAddress)null!, 5003))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpAddressValid",
                    "SimpleTcpClient(IPAddress,int) stores address and port",
                    () =>
                    {
                        using SimpleTcpClient client = new(IPAddress.Loopback, 5003);
                        TestAssert.False(client.IsConnected, "Client should start disconnected.");
                        TestAssert.Equal("127.0.0.1:5003", client.ServerIpPort, "ServerIpPort should reflect the IPAddress constructor.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "HostnameX509NullThrows",
                    "SimpleTcpClient(string,int,X509Certificate2) rejects null certificate",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient("127.0.0.1", 5004, (X509Certificate2)null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "HostnameBytesNullThrows",
                    "SimpleTcpClient(string,int,byte[]) rejects null certificate",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient("127.0.0.1", 5004, (byte[])null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpAddressX509NullThrows",
                    "SimpleTcpClient(IPAddress,int,X509Certificate2) rejects null certificate",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient(IPAddress.Loopback, 5005, (X509Certificate2)null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpAddressBytesNullThrows",
                    "SimpleTcpClient(IPAddress,int,byte[]) rejects null certificate",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient(IPAddress.Loopback, 5005, (byte[])null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "EndpointX509NullThrows",
                    "SimpleTcpClient(IPEndPoint,X509Certificate2) rejects null certificate",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient(
                            new IPEndPoint(IPAddress.Loopback, 5006),
                            (X509Certificate2)null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "EndpointBytesNullThrows",
                    "SimpleTcpClient(IPEndPoint,byte[]) rejects null certificate",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpClient(
                            new IPEndPoint(IPAddress.Loopback, 5006),
                            (byte[])null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "SettingsNullResets",
                    "SimpleTcpClient.Settings resets to defaults when set to null",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:5007");
                        client.Settings = new SimpleTcpClientSettings { ConnectTimeoutMs = 2500 };
                        client.Settings = null!;

                        TestAssert.NotNull(client.Settings, "Settings should never be null.");
                        TestAssert.Equal(5000, client.Settings.ConnectTimeoutMs, "Reset settings should restore default timeout.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "EventsNullResets",
                    "SimpleTcpClient.Events resets to defaults when set to null",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:5008");
                        SimpleTcpClientEvents original = client.Events;
                        client.Events = null!;

                        TestAssert.NotNull(client.Events, "Events should never be null.");
                        TestAssert.NotEqual(original, client.Events, "Resetting events should create a new event container.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "KeepaliveNullResets",
                    "SimpleTcpClient.Keepalive resets to defaults when set to null",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:5009");
                        client.Keepalive = new SimpleTcpKeepaliveSettings { TcpKeepAliveTime = 10 };
                        client.Keepalive = null!;

                        TestAssert.NotNull(client.Keepalive, "Keepalive settings should never be null.");
                        TestAssert.Equal(2, client.Keepalive.TcpKeepAliveTime, "Reset keepalive settings should restore defaults.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "LocalEndpointDisconnectedNull",
                    "SimpleTcpClient.LocalEndpoint is null before connect",
                    () =>
                    {
                        using SimpleTcpClient client = new("127.0.0.1:5010");
                        TestAssert.Null(client.LocalEndpoint, "LocalEndpoint should be null before a connection is established.");
                    }),
            });
    }

    public static TestSuiteDescriptor ServerConstructorSuite()
    {
        const string suiteId = "ServerConstructors";

        return new TestSuiteDescriptor(
            suiteId,
            "Server Constructors",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortNullThrows",
                    "SimpleTcpServer(string) rejects null",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpServer((string)null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortEmptyThrows",
                    "SimpleTcpServer(string) rejects empty",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpServer(string.Empty))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortValid",
                    "SimpleTcpServer(string) starts with listening disabled",
                    () =>
                    {
                        using SimpleTcpServer server = new("127.0.0.1:6000");
                        TestAssert.False(server.IsListening, "Server should not listen before Start().");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ListenerPortNegativeThrows",
                    "SimpleTcpServer(string,int) rejects negative port",
                    () => TestAssert.Throws<ArgumentException>(
                        () => new SimpleTcpServer("127.0.0.1", -1))),

                TestCaseFactory.Sync(
                    suiteId,
                    "NullListenerDefaultsLoopback",
                    "SimpleTcpServer(null,int) defaults to loopback",
                    () =>
                    {
                        using SimpleTcpServer server = new(null!, 6001);
                        TestAssert.Equal(IPAddress.Loopback, server.IpAddress, "Null listener IP should map to loopback.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "WildcardListenerUsesAny",
                    "SimpleTcpServer(*,int) maps to IPAddress.Any",
                    () =>
                    {
                        using SimpleTcpServer server = new("*", 6002);
                        TestAssert.Equal(IPAddress.Any, server.IpAddress, "Wildcard listener should map to IPAddress.Any.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ListenerPortValid",
                    "SimpleTcpServer(string,int) starts with listening disabled",
                    () =>
                    {
                        using SimpleTcpServer server = new("127.0.0.1", 6003);
                        TestAssert.False(server.IsListening, "Server should not listen before Start().");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ListenerPortSslNegativeThrows",
                    "SimpleTcpServer(string,int,bool,string,string) rejects negative port",
                    () => TestAssert.Throws<ArgumentException>(
                        () => new SimpleTcpServer("127.0.0.1", -1, false, null!, null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "ByteCertificateNullThrows",
                    "SimpleTcpServer(string,int,byte[]) rejects null certificate",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpServer("127.0.0.1", 6004, (byte[])null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "ByteCertificateNegativePortThrows",
                    "SimpleTcpServer(string,int,byte[]) rejects negative port",
                    () => TestAssert.Throws<ArgumentException>(
                        () => new SimpleTcpServer("127.0.0.1", -1, new byte[] { 1 }))),

                TestCaseFactory.Sync(
                    suiteId,
                    "IpPortSslNullThrows",
                    "SimpleTcpServer(string,bool,string,string) rejects null ip:port",
                    () => TestAssert.Throws<ArgumentNullException>(
                        () => new SimpleTcpServer((string)null!, false, null!, null!))),

                TestCaseFactory.Sync(
                    suiteId,
                    "SettingsNullResets",
                    "SimpleTcpServer.Settings resets to defaults when set to null",
                    () =>
                    {
                        using SimpleTcpServer server = new("127.0.0.1", 6005);
                        server.Settings = new SimpleTcpServerSettings { MaxConnections = 12 };
                        server.Settings = null!;

                        TestAssert.NotNull(server.Settings, "Settings should never be null.");
                        TestAssert.Equal(4096, server.Settings.MaxConnections, "Reset settings should restore default MaxConnections.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "EventsNullResets",
                    "SimpleTcpServer.Events resets to defaults when set to null",
                    () =>
                    {
                        using SimpleTcpServer server = new("127.0.0.1", 6006);
                        SimpleTcpServerEvents original = server.Events;
                        server.Events = null!;

                        TestAssert.NotNull(server.Events, "Events should never be null.");
                        TestAssert.NotEqual(original, server.Events, "Resetting events should create a new event container.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "KeepaliveNullResets",
                    "SimpleTcpServer.Keepalive resets to defaults when set to null",
                    () =>
                    {
                        using SimpleTcpServer server = new("127.0.0.1", 6007);
                        server.Keepalive = new SimpleTcpKeepaliveSettings { TcpKeepAliveInterval = 10 };
                        server.Keepalive = null!;

                        TestAssert.NotNull(server.Keepalive, "Keepalive settings should never be null.");
                        TestAssert.Equal(2, server.Keepalive.TcpKeepAliveInterval, "Reset keepalive settings should restore defaults.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "EndpointBeforeStartIsNull",
                    "SimpleTcpServer.Endpoint is null before start",
                    () =>
                    {
                        using SimpleTcpServer server = new("127.0.0.1", 0);
                        TestAssert.Null(server.Endpoint, "Endpoint should be null before Start().");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "PortBeforeStartIsZero",
                    "SimpleTcpServer.Port is zero before start when using an ephemeral port",
                    () =>
                    {
                        using SimpleTcpServer server = new("127.0.0.1", 0);
                        TestAssert.Equal(0, server.Port, "Port should be zero before starting an ephemeral listener.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "TooHighPortThrows",
                    "SimpleTcpServer rejects ports above 65535 when starting",
                    () => TestAssert.Throws<ArgumentOutOfRangeException>(
                        () =>
                        {
                            using SimpleTcpServer server = new("127.0.0.1:65536");
                            server.Start();
                        })),

                TestCaseFactory.Sync(
                    suiteId,
                    "CorruptPortThrows",
                    "SimpleTcpServer rejects non-numeric ports",
                    () => TestAssert.Throws<FormatException>(
                        () =>
                        {
                            using SimpleTcpServer server = new("127.0.0.1:INVALID_PORT");
                            server.Start();
                        })),

                TestCaseFactory.Sync(
                    suiteId,
                    "OverflowPortThrows",
                    "SimpleTcpServer rejects overflowing port values",
                    () => TestAssert.Throws<OverflowException>(
                        () =>
                        {
                            using SimpleTcpServer server = new("127.0.0.1:2147483648");
                            server.Start();
                        })),
            });
    }
}
