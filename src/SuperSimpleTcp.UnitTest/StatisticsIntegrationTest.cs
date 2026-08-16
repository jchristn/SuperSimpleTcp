namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Text;
    using System.Threading.Tasks;

    [TestClass]
    public class StatisticsIntegrationTest
    {
        private const string LoopbackIp = "127.0.0.1";

        [TestMethod]
        public async Task Statistics_AfterBidirectionalTraffic_CountersIncrement()
        {
            var payload = Encoding.UTF8.GetBytes(StringHelper.RandomString(2048));
            var ack = Encoding.UTF8.GetBytes("ack");

            var serverReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var clientReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var server = new SimpleTcpServer(LoopbackIp, 0);
            server.Events.DataReceived += (sender, e) =>
            {
                ((SimpleTcpServer)sender!).Send(e.IpPort, ack);
                serverReceived.TrySetResult(true);
            };
            server.Start();
            var port = server.Port;

            using var client = new SimpleTcpClient($"{LoopbackIp}:{port}");
            client.Events.DataReceived += (_, _) => clientReceived.TrySetResult(true);
            client.Connect();

            client.Send(payload);

            await WithTimeout(serverReceived.Task, TimeSpan.FromSeconds(5));
            await WithTimeout(clientReceived.Task, TimeSpan.FromSeconds(5));

            // Give the DataSent bookkeeping a moment to settle on both peers.
            await Task.Delay(100);

            Assert.IsTrue(client.Statistics.SentBytes >= payload.Length,
                $"Client SentBytes ({client.Statistics.SentBytes}) should be at least the payload length ({payload.Length}).");
            Assert.IsTrue(client.Statistics.ReceivedBytes >= ack.Length,
                $"Client ReceivedBytes ({client.Statistics.ReceivedBytes}) should be at least the ack length ({ack.Length}).");
            Assert.IsTrue(server.Statistics.ReceivedBytes >= payload.Length,
                $"Server ReceivedBytes ({server.Statistics.ReceivedBytes}) should be at least the payload length ({payload.Length}).");
            Assert.IsTrue(server.Statistics.SentBytes >= ack.Length,
                $"Server SentBytes ({server.Statistics.SentBytes}) should be at least the ack length ({ack.Length}).");

            client.Disconnect();
            server.Stop();
        }

        [TestMethod]
        public async Task Statistics_Reset_ZeroesCountersAfterTraffic()
        {
            var payload = Encoding.UTF8.GetBytes(StringHelper.RandomString(1024));
            var serverReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var server = new SimpleTcpServer(LoopbackIp, 0);
            server.Events.DataReceived += (_, _) => serverReceived.TrySetResult(true);
            server.Start();
            var port = server.Port;

            using var client = new SimpleTcpClient($"{LoopbackIp}:{port}");
            client.Connect();
            client.Send(payload);

            await WithTimeout(serverReceived.Task, TimeSpan.FromSeconds(5));
            await Task.Delay(100);

            Assert.IsTrue(client.Statistics.SentBytes > 0, "Client should have recorded sent bytes before reset.");
            Assert.IsTrue(server.Statistics.ReceivedBytes > 0, "Server should have recorded received bytes before reset.");

            client.Statistics.Reset();
            server.Statistics.Reset();

            Assert.AreEqual(0, client.Statistics.SentBytes);
            Assert.AreEqual(0, client.Statistics.ReceivedBytes);
            Assert.AreEqual(0, server.Statistics.SentBytes);
            Assert.AreEqual(0, server.Statistics.ReceivedBytes);

            client.Disconnect();
            server.Stop();
        }

        private static async Task WithTimeout(Task task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
                Assert.Fail($"Operation did not complete within {timeout.TotalMilliseconds}ms.");
            await task.ConfigureAwait(false);
        }
    }
}
