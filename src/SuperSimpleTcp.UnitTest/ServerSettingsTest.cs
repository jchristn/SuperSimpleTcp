namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class ServerSettingsTest
    {
        [TestMethod]
        public async Task UseHandleDataReceivedWorkerTask_Enabled_DifferentThreads()
        {
            TestDataReceiver dataReceiver = new ();

            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            simpleTcpServer.Settings.UseAsyncDataReceivedEvents = true;
            simpleTcpServer.Events.DataReceived += dataReceiver.DataReceived;
            simpleTcpServer.Start();
            var port = simpleTcpServer.Port;

            using var simpleTcpClient = new SimpleTcpClient("127.0.0.1", port);
            simpleTcpClient.Connect();

            // Send some data
            for (int i = 0; i < 100; i++)
            {
                await simpleTcpClient.SendAsync($"Message {i}");
                await Task.Delay(10);
            }

            // Wait before closing the server
            await Task.Delay(1000);

            simpleTcpClient.Disconnect();
            simpleTcpServer.Stop();

            // Check if the thread ids were all the same
            Assert.IsTrue(dataReceiver.CallingThreadIds.Distinct().Count() != 1);
        }

        [TestMethod]
        public async Task UseHandleDataReceivedWorkerTask_Disable_Sequential()
        {
            TestDataReceiver dataReceiver = new();

            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            simpleTcpServer.Settings.UseAsyncDataReceivedEvents = false;
            simpleTcpServer.Events.DataReceived += dataReceiver.DataReceived;
            simpleTcpServer.Start();
            var port = simpleTcpServer.Port;

            using var simpleTcpClient = new SimpleTcpClient("127.0.0.1", port);
            simpleTcpClient.Connect();

            // Send some data
            for (int i = 0; i < 100; i++)
            {
                await simpleTcpClient.SendAsync($"Message {i}");
                await Task.Delay(10);
            }

            // Wait before closing the server
            await Task.Delay(1000);

            simpleTcpClient.Disconnect();
            simpleTcpServer.Stop();

            // Check that events were never handled concurrently
            Assert.IsFalse(dataReceiver.ConcurrencyDetected, "Events should execute sequentially when UseAsyncDataReceivedEvents is false");
        }

        /// <summary>
        /// Test class that captures threading info when the DataReceived method is called.
        /// </summary>
        private class TestDataReceiver
        {
            private readonly List<int> _callingThreads = new();
            private int _activeCount;

            /// <summary>
            /// Gets the calling thread ids.
            /// </summary>
            public List<int> CallingThreadIds => _callingThreads;

            /// <summary>
            /// Gets whether concurrent execution was detected.
            /// </summary>
            public bool ConcurrencyDetected { get; private set; }

            public void DataReceived(object? sender, DataReceivedEventArgs args)
            {
                if (Interlocked.Increment(ref _activeCount) > 1)
                    ConcurrencyDetected = true;

                lock (_callingThreads)
                {
                    _callingThreads.Add(Thread.CurrentThread.ManagedThreadId);
                }

                Interlocked.Decrement(ref _activeCount);
            }
        }
    }
}
