namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class ServerSettingsTest
    {
        private const int MessageCount = 100;

        // ---------------------------------------------------------------------
        // Positive tests: with UseAsyncDataReceivedEvents enabled (the default),
        // handlers must be invoked one-at-a-time and in the exact order data was
        // received, so message reassembly is never corrupted. See issue #236.
        // ---------------------------------------------------------------------

        [TestMethod]
        public async Task AsyncEnabled_Server_PreservesOrderAndDoesNotOverlap()
        {
            OrderedDataReceiver receiver = new();

            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            Assert.IsTrue(simpleTcpServer.Settings.UseAsyncDataReceivedEvents,
                "Async data received events should be enabled by default.");
            simpleTcpServer.Events.DataReceived += receiver.DataReceived;
            simpleTcpServer.Start();
            var port = simpleTcpServer.Port;

            using var simpleTcpClient = new SimpleTcpClient("127.0.0.1", port);
            simpleTcpClient.Connect();

            string expected = await SendSequentialMessagesAsync(m => simpleTcpClient.SendAsync(m));

            await receiver.WaitForBytesAsync(expected.Length, TimeSpan.FromSeconds(15));

            simpleTcpClient.Disconnect();
            simpleTcpServer.Stop();

            Assert.IsFalse(receiver.ConcurrencyDetected,
                "DataReceived handlers must never overlap when async events are enabled (single-worker dispatch).");
            Assert.AreEqual(expected, receiver.Assembled,
                "Async DataReceived handlers must deliver bytes in the exact order they were received.");
        }

        [TestMethod]
        public async Task AsyncEnabled_Client_PreservesOrderAndDoesNotOverlap()
        {
            OrderedDataReceiver receiver = new();

            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            simpleTcpServer.Start();
            var port = simpleTcpServer.Port;

            using var simpleTcpClient = new SimpleTcpClient("127.0.0.1", port);
            Assert.IsTrue(simpleTcpClient.Settings.UseAsyncDataReceivedEvents,
                "Async data received events should be enabled by default.");
            simpleTcpClient.Events.DataReceived += receiver.DataReceived;
            simpleTcpClient.Connect();

            // Wait for the server to register the client connection.
            await Task.Delay(250);
            string clientIpPort = GetSingleClient(simpleTcpServer);

            string expected = await SendSequentialMessagesAsync(m => simpleTcpServer.SendAsync(clientIpPort, m));

            await receiver.WaitForBytesAsync(expected.Length, TimeSpan.FromSeconds(15));

            simpleTcpClient.Disconnect();
            simpleTcpServer.Stop();

            Assert.IsFalse(receiver.ConcurrencyDetected,
                "DataReceived handlers must never overlap when async events are enabled (single-worker dispatch).");
            Assert.AreEqual(expected, receiver.Assembled,
                "Async DataReceived handlers must deliver bytes in the exact order they were received.");
        }

        // ---------------------------------------------------------------------
        // Negative tests: handlers must never execute concurrently, both when
        // async events are disabled (fired inline on the receive loop) and when
        // enabled (fired from the single dispatch worker).
        // ---------------------------------------------------------------------

        [TestMethod]
        public async Task AsyncDisabled_Server_Sequential()
        {
            OrderedDataReceiver receiver = new();

            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            simpleTcpServer.Settings.UseAsyncDataReceivedEvents = false;
            simpleTcpServer.Events.DataReceived += receiver.DataReceived;
            simpleTcpServer.Start();
            var port = simpleTcpServer.Port;

            using var simpleTcpClient = new SimpleTcpClient("127.0.0.1", port);
            simpleTcpClient.Connect();

            string expected = await SendSequentialMessagesAsync(m => simpleTcpClient.SendAsync(m));

            await receiver.WaitForBytesAsync(expected.Length, TimeSpan.FromSeconds(15));

            simpleTcpClient.Disconnect();
            simpleTcpServer.Stop();

            Assert.IsFalse(receiver.ConcurrencyDetected,
                "Events should execute sequentially when UseAsyncDataReceivedEvents is false.");
            Assert.AreEqual(expected, receiver.Assembled,
                "Bytes should be delivered in order when UseAsyncDataReceivedEvents is false.");
        }

        private static async Task<string> SendSequentialMessagesAsync(Func<string, Task> sendAsync)
        {
            StringBuilder expected = new();
            for (int i = 0; i < MessageCount; i++)
            {
                string message = $"Message {i};";
                expected.Append(message);
                await sendAsync(message);
                await Task.Delay(10);
            }

            return expected.ToString();
        }

        private static string GetSingleClient(SimpleTcpServer server)
        {
            foreach (string client in server.GetClients())
            {
                return client;
            }

            throw new InvalidOperationException("No client connected to the server.");
        }

        /// <summary>
        /// Captures DataReceived invocations: detects any concurrent (overlapping) execution
        /// and assembles received bytes in the exact order handlers were invoked, so that any
        /// reordering corrupts the reassembled stream and fails the test.
        /// </summary>
        private sealed class OrderedDataReceiver
        {
            private readonly List<byte> _assembled = new();
            private readonly TaskCompletionSource<bool> _completed =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _activeCount;
            private int _invocations;
            private int _targetBytes = int.MaxValue;

            /// <summary>
            /// Gets whether two handlers were ever observed executing at the same time.
            /// </summary>
            public bool ConcurrencyDetected { get; private set; }

            /// <summary>
            /// Gets the bytes assembled in handler-invocation order, decoded as UTF-8.
            /// </summary>
            public string Assembled
            {
                get
                {
                    lock (_assembled)
                    {
                        return Encoding.UTF8.GetString(_assembled.ToArray());
                    }
                }
            }

            public void DataReceived(object? sender, DataReceivedEventArgs args)
            {
                int invocation = Interlocked.Increment(ref _invocations);

                if (Interlocked.Increment(ref _activeCount) > 1)
                    ConcurrencyDetected = true;

                // Block the first invocation long enough for later segments to queue up behind it.
                // Under single-worker dispatch the queue drains strictly in order; under multi-worker
                // dispatch other workers would pick up the queued segments and run concurrently,
                // overlapping here and scrambling the assembled byte stream below.
                Thread.Sleep(invocation == 1 ? 300 : 2);

                lock (_assembled)
                {
                    ArraySegment<byte> data = args.Data;
                    if (data.Array != null)
                    {
                        for (int i = 0; i < data.Count; i++)
                        {
                            _assembled.Add(data.Array[data.Offset + i]);
                        }
                    }

                    if (_assembled.Count >= _targetBytes)
                        _completed.TrySetResult(true);
                }

                Interlocked.Decrement(ref _activeCount);
            }

            public async Task WaitForBytesAsync(int targetBytes, TimeSpan timeout)
            {
                lock (_assembled)
                {
                    _targetBytes = targetBytes;
                    if (_assembled.Count >= _targetBytes)
                        _completed.TrySetResult(true);
                }

                await Task.WhenAny(_completed.Task, Task.Delay(timeout));
            }
        }
    }
}
