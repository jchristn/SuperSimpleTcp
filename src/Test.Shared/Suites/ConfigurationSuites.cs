namespace Test.Shared.Suites;

using System.Net;
using System.Net.Security;

using SuperSimpleTcp;

using Touchstone.Core;

internal static class ConfigurationSuites
{
    public static TestSuiteDescriptor ClientSettingsSuite()
    {
        const string suiteId = "ClientSettings";

        return new TestSuiteDescriptor(
            suiteId,
            "Client Settings",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "Defaults",
                    "SimpleTcpClientSettings exposes documented defaults",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();

                        TestAssert.Equal(true, settings.NoDelay, "NoDelay should default to true.");
                        TestAssert.Equal(65536, settings.StreamBufferSize, "StreamBufferSize default mismatch.");
                        TestAssert.Equal(5000, settings.ConnectTimeoutMs, "ConnectTimeoutMs default mismatch.");
                        TestAssert.Equal(1000, settings.ReadTimeoutMs, "ReadTimeoutMs default mismatch.");
                        TestAssert.Equal(0, settings.IdleServerTimeoutMs, "IdleServerTimeoutMs default mismatch.");
                        TestAssert.Equal(1000, settings.IdleServerEvaluationIntervalMs, "IdleServerEvaluationIntervalMs default mismatch.");
                        TestAssert.Equal(200, settings.ConnectionLostEvaluationIntervalMs, "ConnectionLostEvaluationIntervalMs default mismatch.");
                        TestAssert.Equal(true, settings.AcceptInvalidCertificates, "AcceptInvalidCertificates should default to true.");
                        TestAssert.Equal(true, settings.MutuallyAuthenticate, "MutuallyAuthenticate should default to true.");
                        TestAssert.Equal(true, settings.UseAsyncDataReceivedEvents, "UseAsyncDataReceivedEvents should default to true.");
                        TestAssert.Equal(true, settings.CheckCertificateRevocation, "CheckCertificateRevocation should default to true.");
                        TestAssert.Null(settings.LocalEndpoint, "LocalEndpoint should default to null.");
                        TestAssert.Null(settings.CertificateValidationCallback, "CertificateValidationCallback should default to null.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "LocalEndpointRoundTrip",
                    "SimpleTcpClientSettings.LocalEndpoint stores values",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        IPEndPoint endpoint = new(IPAddress.Loopback, 12345);
                        settings.LocalEndpoint = endpoint;
                        TestAssert.Equal(endpoint, settings.LocalEndpoint, "LocalEndpoint should round-trip assigned values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "NoDelayRoundTrip",
                    "SimpleTcpClientSettings.NoDelay stores values",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        settings.NoDelay = false;
                        TestAssert.False(settings.NoDelay, "NoDelay should store assigned values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SecurityFlagsRoundTrip",
                    "SimpleTcpClientSettings security fields are mutable",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        RemoteCertificateValidationCallback callback = (_, _, _, _) => true;

                        settings.AcceptInvalidCertificates = false;
                        settings.MutuallyAuthenticate = false;
                        settings.UseAsyncDataReceivedEvents = false;
                        settings.CheckCertificateRevocation = false;
                        settings.CertificateValidationCallback = callback;

                        TestAssert.False(settings.AcceptInvalidCertificates, "AcceptInvalidCertificates should be mutable.");
                        TestAssert.False(settings.MutuallyAuthenticate, "MutuallyAuthenticate should be mutable.");
                        TestAssert.False(settings.UseAsyncDataReceivedEvents, "UseAsyncDataReceivedEvents should be mutable.");
                        TestAssert.False(settings.CheckCertificateRevocation, "CheckCertificateRevocation should be mutable.");
                        TestAssert.Equal(callback, settings.CertificateValidationCallback, "CertificateValidationCallback should store assigned delegates.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "StreamBufferSizeValidation",
                    "SimpleTcpClientSettings.StreamBufferSize validates bounds",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.StreamBufferSize = 0);
                        TestAssert.Throws<ArgumentException>(() => settings.StreamBufferSize = -1);
                        TestAssert.Throws<ArgumentException>(() => settings.StreamBufferSize = 65537);

                        settings.StreamBufferSize = 1024;
                        TestAssert.Equal(1024, settings.StreamBufferSize, "StreamBufferSize should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ConnectTimeoutValidation",
                    "SimpleTcpClientSettings.ConnectTimeoutMs validates bounds",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.ConnectTimeoutMs = 0);
                        TestAssert.Throws<ArgumentException>(() => settings.ConnectTimeoutMs = -1);

                        settings.ConnectTimeoutMs = 3000;
                        TestAssert.Equal(3000, settings.ConnectTimeoutMs, "ConnectTimeoutMs should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ReadTimeoutValidation",
                    "SimpleTcpClientSettings.ReadTimeoutMs validates bounds",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.ReadTimeoutMs = 0);

                        settings.ReadTimeoutMs = 500;
                        TestAssert.Equal(500, settings.ReadTimeoutMs, "ReadTimeoutMs should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "IdleServerTimeoutValidation",
                    "SimpleTcpClientSettings.IdleServerTimeoutMs validates bounds",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.IdleServerTimeoutMs = -1);

                        settings.IdleServerTimeoutMs = 30000;
                        TestAssert.Equal(30000, settings.IdleServerTimeoutMs, "IdleServerTimeoutMs should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "IdleServerEvaluationValidation",
                    "SimpleTcpClientSettings.IdleServerEvaluationIntervalMs validates bounds",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        TestAssert.Throws<ArgumentOutOfRangeException>(() => settings.IdleServerEvaluationIntervalMs = 0);

                        settings.IdleServerEvaluationIntervalMs = 2000;
                        TestAssert.Equal(2000, settings.IdleServerEvaluationIntervalMs, "IdleServerEvaluationIntervalMs should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ConnectionLostEvaluationValidation",
                    "SimpleTcpClientSettings.ConnectionLostEvaluationIntervalMs validates bounds",
                    () =>
                    {
                        SimpleTcpClientSettings settings = new();
                        TestAssert.Throws<ArgumentOutOfRangeException>(() => settings.ConnectionLostEvaluationIntervalMs = 0);

                        settings.ConnectionLostEvaluationIntervalMs = 500;
                        TestAssert.Equal(500, settings.ConnectionLostEvaluationIntervalMs, "ConnectionLostEvaluationIntervalMs should accept valid values.");
                    }),
            });
    }

    public static TestSuiteDescriptor ServerSettingsSuite()
    {
        const string suiteId = "ServerSettings";

        return new TestSuiteDescriptor(
            suiteId,
            "Server Settings",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "Defaults",
                    "SimpleTcpServerSettings exposes documented defaults",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();

                        TestAssert.Equal(true, settings.NoDelay, "NoDelay should default to true.");
                        TestAssert.Equal(65536, settings.StreamBufferSize, "StreamBufferSize default mismatch.");
                        TestAssert.Equal(4096, settings.MaxConnections, "MaxConnections default mismatch.");
                        TestAssert.Equal(0, settings.IdleClientTimeoutMs, "IdleClientTimeoutMs default mismatch.");
                        TestAssert.Equal(5000, settings.IdleClientEvaluationIntervalMs, "IdleClientEvaluationIntervalMs default mismatch.");
                        TestAssert.Equal(true, settings.AcceptInvalidCertificates, "AcceptInvalidCertificates should default to true.");
                        TestAssert.Equal(true, settings.MutuallyAuthenticate, "MutuallyAuthenticate should default to true.");
                        TestAssert.Equal(true, settings.UseAsyncDataReceivedEvents, "UseAsyncDataReceivedEvents should default to true.");
                        TestAssert.Equal(true, settings.CheckCertificateRevocation, "CheckCertificateRevocation should default to true.");
                        TestAssert.Equal(0, settings.PermittedIPs.Count, "PermittedIPs should default to empty.");
                        TestAssert.Equal(0, settings.BlockedIPs.Count, "BlockedIPs should default to empty.");
                        TestAssert.Null(settings.CertificateValidationCallback, "CertificateValidationCallback should default to null.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "NoDelayRoundTrip",
                    "SimpleTcpServerSettings.NoDelay stores values",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        settings.NoDelay = false;
                        TestAssert.False(settings.NoDelay, "NoDelay should store assigned values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "SecurityFlagsRoundTrip",
                    "SimpleTcpServerSettings security fields are mutable",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        RemoteCertificateValidationCallback callback = (_, _, _, _) => true;

                        settings.AcceptInvalidCertificates = false;
                        settings.MutuallyAuthenticate = false;
                        settings.UseAsyncDataReceivedEvents = false;
                        settings.CheckCertificateRevocation = false;
                        settings.CertificateValidationCallback = callback;

                        TestAssert.False(settings.AcceptInvalidCertificates, "AcceptInvalidCertificates should be mutable.");
                        TestAssert.False(settings.MutuallyAuthenticate, "MutuallyAuthenticate should be mutable.");
                        TestAssert.False(settings.UseAsyncDataReceivedEvents, "UseAsyncDataReceivedEvents should be mutable.");
                        TestAssert.False(settings.CheckCertificateRevocation, "CheckCertificateRevocation should be mutable.");
                        TestAssert.Equal(callback, settings.CertificateValidationCallback, "CertificateValidationCallback should store assigned delegates.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "StreamBufferSizeValidation",
                    "SimpleTcpServerSettings.StreamBufferSize validates bounds",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.StreamBufferSize = 0);
                        TestAssert.Throws<ArgumentException>(() => settings.StreamBufferSize = 65537);

                        settings.StreamBufferSize = 65536;
                        TestAssert.Equal(65536, settings.StreamBufferSize, "StreamBufferSize should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "MaxConnectionsValidation",
                    "SimpleTcpServerSettings.MaxConnections validates bounds",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.MaxConnections = 0);
                        TestAssert.Throws<ArgumentException>(() => settings.MaxConnections = -1);

                        settings.MaxConnections = 100;
                        TestAssert.Equal(100, settings.MaxConnections, "MaxConnections should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "IdleClientTimeoutValidation",
                    "SimpleTcpServerSettings.IdleClientTimeoutMs validates bounds",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.IdleClientTimeoutMs = -1);

                        settings.IdleClientTimeoutMs = 30000;
                        TestAssert.Equal(30000, settings.IdleClientTimeoutMs, "IdleClientTimeoutMs should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "IdleClientEvaluationValidation",
                    "SimpleTcpServerSettings.IdleClientEvaluationIntervalMs validates bounds",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        TestAssert.Throws<ArgumentOutOfRangeException>(() => settings.IdleClientEvaluationIntervalMs = 0);

                        settings.IdleClientEvaluationIntervalMs = 3000;
                        TestAssert.Equal(3000, settings.IdleClientEvaluationIntervalMs, "IdleClientEvaluationIntervalMs should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "PermittedIpsNullResets",
                    "SimpleTcpServerSettings.PermittedIPs resets to empty when set to null",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        settings.PermittedIPs = null!;

                        TestAssert.NotNull(settings.PermittedIPs, "PermittedIPs should never be null.");
                        TestAssert.Equal(0, settings.PermittedIPs.Count, "PermittedIPs should reset to empty.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "BlockedIpsNullResets",
                    "SimpleTcpServerSettings.BlockedIPs resets to empty when set to null",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        settings.BlockedIPs = null!;

                        TestAssert.NotNull(settings.BlockedIPs, "BlockedIPs should never be null.");
                        TestAssert.Equal(0, settings.BlockedIPs.Count, "BlockedIPs should reset to empty.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ListsRoundTrip",
                    "SimpleTcpServerSettings IP allow/block lists store assigned values",
                    () =>
                    {
                        SimpleTcpServerSettings settings = new();
                        settings.PermittedIPs = new List<string> { "127.0.0.1" };
                        settings.BlockedIPs = new List<string> { "10.0.0.1" };

                        TestAssert.Equal(1, settings.PermittedIPs.Count, "PermittedIPs should store assigned values.");
                        TestAssert.Equal("127.0.0.1", settings.PermittedIPs[0], "PermittedIPs should preserve contents.");
                        TestAssert.Equal(1, settings.BlockedIPs.Count, "BlockedIPs should store assigned values.");
                        TestAssert.Equal("10.0.0.1", settings.BlockedIPs[0], "BlockedIPs should preserve contents.");
                    }),
            });
    }

    public static TestSuiteDescriptor KeepaliveSuite()
    {
        const string suiteId = "Keepalive";

        return new TestSuiteDescriptor(
            suiteId,
            "Keepalive Settings",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "Defaults",
                    "SimpleTcpKeepaliveSettings exposes documented defaults",
                    () =>
                    {
                        SimpleTcpKeepaliveSettings settings = new();
                        TestAssert.False(settings.EnableTcpKeepAlives, "EnableTcpKeepAlives should default to false.");
                        TestAssert.Equal(2, settings.TcpKeepAliveInterval, "TcpKeepAliveInterval default mismatch.");
                        TestAssert.Equal(2, settings.TcpKeepAliveTime, "TcpKeepAliveTime default mismatch.");
                        TestAssert.Equal(3, settings.TcpKeepAliveRetryCount, "TcpKeepAliveRetryCount default mismatch.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "IntervalValidation",
                    "SimpleTcpKeepaliveSettings.TcpKeepAliveInterval validates bounds",
                    () =>
                    {
                        SimpleTcpKeepaliveSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.TcpKeepAliveInterval = 0);
                        TestAssert.Throws<ArgumentException>(() => settings.TcpKeepAliveInterval = -1);

                        settings.TcpKeepAliveInterval = 10;
                        TestAssert.Equal(10, settings.TcpKeepAliveInterval, "TcpKeepAliveInterval should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "TimeValidation",
                    "SimpleTcpKeepaliveSettings.TcpKeepAliveTime validates bounds",
                    () =>
                    {
                        SimpleTcpKeepaliveSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.TcpKeepAliveTime = 0);
                        TestAssert.Throws<ArgumentException>(() => settings.TcpKeepAliveTime = -1);

                        settings.TcpKeepAliveTime = 15;
                        TestAssert.Equal(15, settings.TcpKeepAliveTime, "TcpKeepAliveTime should accept valid values.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "RetryValidation",
                    "SimpleTcpKeepaliveSettings.TcpKeepAliveRetryCount validates bounds",
                    () =>
                    {
                        SimpleTcpKeepaliveSettings settings = new();
                        TestAssert.Throws<ArgumentException>(() => settings.TcpKeepAliveRetryCount = 0);
                        TestAssert.Throws<ArgumentException>(() => settings.TcpKeepAliveRetryCount = -1);

                        settings.EnableTcpKeepAlives = true;
                        settings.TcpKeepAliveRetryCount = 5;
                        TestAssert.True(settings.EnableTcpKeepAlives, "EnableTcpKeepAlives should be mutable.");
                        TestAssert.Equal(5, settings.TcpKeepAliveRetryCount, "TcpKeepAliveRetryCount should accept valid values.");
                    }),
            });
    }

    public static TestSuiteDescriptor StatisticsSuite()
    {
        const string suiteId = "Statistics";

        return new TestSuiteDescriptor(
            suiteId,
            "Statistics",
            new List<TestCaseDescriptor>
            {
                TestCaseFactory.Sync(
                    suiteId,
                    "Defaults",
                    "SimpleTcpStatistics starts from a clean baseline",
                    () =>
                    {
                        DateTime before = DateTime.UtcNow;
                        SimpleTcpStatistics stats = new();
                        DateTime after = DateTime.UtcNow;

                        TestAssert.True(stats.StartTime >= before.AddSeconds(-1), "StartTime should be recent.");
                        TestAssert.True(stats.StartTime <= after.AddSeconds(1), "StartTime should be recent.");
                        TestAssert.True(stats.UpTime >= TimeSpan.Zero, "UpTime should never be negative.");
                        TestAssert.Equal(0L, stats.SentBytes, "SentBytes should start at zero.");
                        TestAssert.Equal(0L, stats.ReceivedBytes, "ReceivedBytes should start at zero.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ToStringContainsLabels",
                    "SimpleTcpStatistics.ToString includes key fields",
                    () =>
                    {
                        SimpleTcpStatistics stats = new();
                        string value = stats.ToString();

                        TestAssert.Contains("Statistics", value, "ToString should contain the header.");
                        TestAssert.Contains("Started", value, "ToString should contain StartTime.");
                        TestAssert.Contains("Uptime", value, "ToString should contain UpTime.");
                        TestAssert.Contains("Received bytes", value, "ToString should contain ReceivedBytes.");
                        TestAssert.Contains("Sent bytes", value, "ToString should contain SentBytes.");
                    }),

                TestCaseFactory.Sync(
                    suiteId,
                    "ResetLeavesZeros",
                    "SimpleTcpStatistics.Reset clears counters",
                    () =>
                    {
                        SimpleTcpStatistics stats = new();
                        stats.Reset();

                        TestAssert.Equal(0L, stats.SentBytes, "SentBytes should remain zero after reset.");
                        TestAssert.Equal(0L, stats.ReceivedBytes, "ReceivedBytes should remain zero after reset.");
                    }),

                TestCaseFactory.Async(
                    suiteId,
                    "RoundTripUpdatesCounters",
                    "Client and server statistics increment during traffic and reset cleanly",
                    async token =>
                    {
                        using SimpleTcpServer server = new(TestEnvironment.LoopbackIp, 0);
                        TaskCompletionSource<bool> serverReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        server.Events.DataReceived += (_, _) => serverReceived.TrySetResult(true);
                        server.Start();

                        using SimpleTcpClient client = new($"{TestEnvironment.LoopbackIp}:{server.Port}");
                        client.Connect();
                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        await client.SendAsync("test data", token).ConfigureAwait(false);
                        await TestEnvironment.WithTimeoutAsync(
                            serverReceived.Task,
                            TimeSpan.FromSeconds(5),
                            "Server did not receive test data.").ConfigureAwait(false);

                        await TestEnvironment.DelayForIoAsync(token).ConfigureAwait(false);

                        TestAssert.True(client.Statistics.SentBytes > 0, "Client SentBytes should increase after sending.");
                        TestAssert.True(server.Statistics.ReceivedBytes > 0, "Server ReceivedBytes should increase after receiving.");

                        client.Statistics.Reset();
                        server.Statistics.Reset();

                        TestAssert.Equal(0L, client.Statistics.SentBytes, "Client SentBytes should reset to zero.");
                        TestAssert.Equal(0L, client.Statistics.ReceivedBytes, "Client ReceivedBytes should reset to zero.");
                        TestAssert.Equal(0L, server.Statistics.SentBytes, "Server SentBytes should reset to zero.");
                        TestAssert.Equal(0L, server.Statistics.ReceivedBytes, "Server ReceivedBytes should reset to zero.");

                        client.Disconnect();
                        server.Stop();
                    }),
            });
    }
}
