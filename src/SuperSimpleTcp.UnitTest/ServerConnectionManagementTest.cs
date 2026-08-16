namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class ServerConnectionManagementTest
    {
        private const string LoopbackIp = "127.0.0.1";

        [TestMethod]
        public async Task MultipleClients_ConnectionsAndGetClients_ReflectActiveClients()
        {
            var connectedCount = 0;

            using var server = new SimpleTcpServer(LoopbackIp, 0);
            server.Events.ClientConnected += (_, _) => Interlocked.Increment(ref connectedCount);
            server.Start();
            var port = server.Port;

            using var client1 = new SimpleTcpClient($"{LoopbackIp}:{port}");
            using var client2 = new SimpleTcpClient($"{LoopbackIp}:{port}");
            using var client3 = new SimpleTcpClient($"{LoopbackIp}:{port}");

            client1.Connect();
            client2.Connect();
            client3.Connect();

            await WaitForConditionAsync(() => Volatile.Read(ref connectedCount) == 3, TimeSpan.FromSeconds(5));

            Assert.AreEqual(3, server.Connections, "Server should report three active connections.");

            var clients = server.GetClients().ToList();
            Assert.AreEqual(3, clients.Count, "GetClients should enumerate all connected clients.");
            foreach (var ipPort in clients)
                Assert.IsTrue(server.IsConnected(ipPort), $"Server should report {ipPort} as connected.");

            client1.Disconnect();
            client2.Disconnect();
            client3.Disconnect();

            await WaitForConditionAsync(() => server.Connections == 0, TimeSpan.FromSeconds(5));

            server.Stop();
        }

        [TestMethod]
        public void IsConnected_UnknownClient_ReturnsFalse()
        {
            using var server = new SimpleTcpServer(LoopbackIp, 0);
            server.Start();

            Assert.IsFalse(server.IsConnected("10.0.0.1:12345"), "Unknown client should not report as connected.");

            server.Stop();
        }

        [TestMethod]
        public async Task DisconnectClient_ConnectedClient_RaisesKickedDisconnect()
        {
            var disconnected = new TaskCompletionSource<ConnectionEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var server = new SimpleTcpServer(LoopbackIp, 0);
            server.Events.ClientConnected += (_, e) => connected.TrySetResult(e.IpPort);
            server.Events.ClientDisconnected += (_, e) => disconnected.TrySetResult(e);
            server.Start();
            var port = server.Port;

            using var client = new SimpleTcpClient($"{LoopbackIp}:{port}");
            client.Connect();

            var clientIpPort = await WithTimeout(connected.Task, TimeSpan.FromSeconds(5));

            server.DisconnectClient(clientIpPort);

            var args = await WithTimeout(disconnected.Task, TimeSpan.FromSeconds(5));
            Assert.AreEqual(DisconnectReason.Kicked, args.Reason,
                "Server-initiated DisconnectClient should surface DisconnectReason.Kicked.");

            server.Stop();
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
                Assert.Fail($"Operation did not complete within {timeout.TotalMilliseconds}ms.");
            return await task.ConfigureAwait(false);
        }

        private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
            while (!condition())
            {
                if (Environment.TickCount64 >= deadline)
                    Assert.Fail($"Condition was not met within {timeout.TotalMilliseconds}ms.");

                await Task.Delay(25).ConfigureAwait(false);
            }
        }
    }
}
