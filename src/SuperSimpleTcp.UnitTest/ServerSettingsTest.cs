using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuperSimpleTcp.UnitTest
{
    [TestClass]
    public class ServerSettingsTest
    {
        [TestMethod]
        public async Task UseHandleDataReceivedWorkerTask_Enabled_DifferentThreads()
        {
            TestDataReceiver dataReceiver = new ();

            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 50000);
            simpleTcpServer.Settings.UseAsyncDataReceivedEvents = true;
            simpleTcpServer.Events.DataReceived += dataReceiver.DataReceived;
            simpleTcpServer.Start();

            using var simpleTcpClient = new SimpleTcpClient("127.0.0.1", 50000);
            simpleTcpClient.Connect();

            // Send some data
            for (int i = 0; i < 100; i++)
            {
                simpleTcpClient.SendAsync($"Message {i}");
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
        public async Task UseHandleDataReceivedWorkerTask_Disable_SameThread()
        {
            TestDataReceiver dataReceiver = new();

            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 50000);
            simpleTcpServer.Settings.UseAsyncDataReceivedEvents = false;
            simpleTcpServer.Events.DataReceived += dataReceiver.DataReceived;
            simpleTcpServer.Start();

            using var simpleTcpClient = new SimpleTcpClient("127.0.0.1", 50000);
            simpleTcpClient.Connect();

            // Send some data
            for (int i = 0; i < 100; i++)
            {
                simpleTcpClient.SendAsync($"Message {i}");
                await Task.Delay(10);
            }

            // Wait before closing the server
            await Task.Delay(1000);

            simpleTcpClient.Disconnect();
            simpleTcpServer.Stop();

            // Check if the thread ids were different
            Assert.IsTrue(dataReceiver.CallingThreadIds.Distinct().Count() == 1);
        }

        /// <summary>
        /// Test class that captures the thread identifiers when the DataReceived method is called.
        /// </summary>
        private class TestDataReceiver
        {
            private readonly List<int> _callingThreads = new();

            /// <summary>
            /// Gets the calling thread ids.
            /// </summary>
            /// <value>The calling thread ids.</value>
            public List<int> CallingThreadIds => _callingThreads;

            public void DataReceived(object? sender, DataReceivedEventArgs args)
            {
                // Get current thread id
                lock (_callingThreads)
                {
                    CallingThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
                }
            }
        }
    }
}
