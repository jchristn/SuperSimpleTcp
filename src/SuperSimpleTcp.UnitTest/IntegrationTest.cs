namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using DataReceivedEventArgs = SuperSimpleTcp.DataReceivedEventArgs;

    [TestClass]
    public class IntegrationTest
    {
        //Unit Test Naming
        //[UnitOfWork_StateUnderTest_ExpectedBehavior]

        [TestMethod]
        public async Task Start_StartServerAndConnectWithOneClient_Successful()
        {
            var ipAddress = "127.0.0.1";
            var port = 8000;

            var expectedClientConnectedCount = 1;
            var clientConnectedCount = 0;

            void ClientConnected(object? sender, ConnectionEventArgs e)
            {
                clientConnectedCount++;
            }

            using var simpleTcpServer = new SimpleTcpServer($"{ipAddress}:{port}");
            simpleTcpServer.Start();
            simpleTcpServer.Events.ClientConnected += ClientConnected;

            using var simpleTcpClient = new SimpleTcpClient($"{ipAddress}:{port}");
            simpleTcpClient.Connect();
            simpleTcpClient.Send("test");
            simpleTcpClient.Disconnect();

            await Task.Delay(10);

            simpleTcpServer.Events.ClientConnected -= ClientConnected;
            simpleTcpServer.Stop();

            Assert.AreEqual(expectedClientConnectedCount, clientConnectedCount);
        }

        [TestMethod]
        public async Task Start_StartServerAndConnectWithOneClientAndSendMessages_Successful()
        {
            var ipAddress = "127.0.0.1";
            var port = 8001;
            var testData = StringHelper.RandomString(1000);
            var acknowledgeData = Encoding.UTF8.GetBytes("acknowledge");

            var serverReceiveError = false;
            var serverReceiveData = false;

            var clientReceiveError = false;
            var clientReceiveData = false;

            var expectedClientConnectedCount = 1;
            var clientConnectedCount = 0;

            void ServerClientConnected(object? sender, ConnectionEventArgs e)
            {
                clientConnectedCount++;
            }

            void ServerDataReceived(object? sender, DataReceivedEventArgs e)
            {
                serverReceiveData = true;

                var receivedData = Encoding.UTF8.GetString(e.Data);
                Trace.WriteLine($"{nameof(ServerDataReceived)} - {receivedData}");

                if (testData != receivedData)
                {
                    serverReceiveError = true;
                }

                if (sender is SimpleTcpServer simpleTcpServer)
                {
                    simpleTcpServer.Send(e.IpPort, acknowledgeData);
                }
            }

            void ClientDataReceived(object? sender, DataReceivedEventArgs e)
            {
                clientReceiveData = true;

                var receivedData = Encoding.UTF8.GetString(e.Data);
                Trace.WriteLine($"{nameof(ClientDataReceived)} - {receivedData}");

                if (!Enumerable.SequenceEqual(e.Data, acknowledgeData))
                {
                    clientReceiveError = true;
                }
            }

            using var simpleTcpServer = new SimpleTcpServer($"{ipAddress}:{port}");
            simpleTcpServer.Start();
            simpleTcpServer.Events.ClientConnected += ServerClientConnected;
            simpleTcpServer.Events.DataReceived += ServerDataReceived;

            using var simpleTcpClient = new SimpleTcpClient($"{ipAddress}:{port}");
            simpleTcpClient.Connect();
            simpleTcpClient.Events.DataReceived += ClientDataReceived;
            for (var i = 0; i < 10; i++)
            {
                simpleTcpClient.Send(testData);
                await Task.Delay(100);
            }
            simpleTcpClient.Events.DataReceived -= ClientDataReceived;
            simpleTcpClient.Disconnect();

            await Task.Delay(10);

            simpleTcpServer.Events.ClientConnected -= ServerClientConnected;
            simpleTcpServer.Events.DataReceived -= ServerDataReceived;
            simpleTcpServer.Stop();

            Assert.AreEqual(expectedClientConnectedCount, clientConnectedCount);
            Assert.IsTrue(serverReceiveData, "Server receive no data");
            Assert.IsFalse(serverReceiveError, "Server receive error detected");

            Assert.IsTrue(clientReceiveData, "Client receive no data");
            Assert.IsFalse(clientReceiveError, "Client receive error detected");
        }

        [TestMethod]
        public async Task Start_ServerShouldProcessesAvailableReceivedDataEvenAfterAClientSideDisconnect_Successful()
        {
            var ipAddress = "127.0.0.1";
            var port = 8001;
            var testData = StringHelper.RandomString(65535);

            var serverReceiveError = false;
            var serverReceivedData = "";

            var clientSendCount = 10;
            var expectedClientConnectedCount = 1;
            var expectedClientData = "";
            var expectedClientDataReceivedBytes = clientSendCount * testData.Length;
            var clientConnectedCount = 0;

            void ServerClientConnected(object? sender, ConnectionEventArgs e)
            {
                clientConnectedCount++;
                // The following delay is used to simulate an issue that is difficult to make happen on the
                // loopback network interface, since it has very low latency. The specific issue happened if
                // a client sent data and then immediately disconnected. If SimpleTcpServer's DataReceived task
                // didn't start executing before the disconnect happened, DataReceived might exit without actually
                // reading any of the data received from that connection.
                Thread.Sleep(10);
            }

            void ServerDataReceived(object? sender, DataReceivedEventArgs e)
            {
                serverReceivedData += Encoding.UTF8.GetString(e.Data);
            }

            using var simpleTcpServer = new SimpleTcpServer($"{ipAddress}:{port}");
            simpleTcpServer.Settings.UseAsyncDataReceivedEvents = false;
            simpleTcpServer.Start();
            simpleTcpServer.Events.ClientConnected += ServerClientConnected;
            simpleTcpServer.Events.DataReceived += ServerDataReceived;

            using var simpleTcpClient = new SimpleTcpClient($"{ipAddress}:{port}");
            simpleTcpClient.Connect();
            for (var i = 0; i < clientSendCount; i++)
            {
                expectedClientData += testData;
                simpleTcpClient.Send(testData);
            }
            simpleTcpClient.Disconnect();

            await Task.Delay(250);

            simpleTcpServer.Events.ClientConnected -= ServerClientConnected;
            simpleTcpServer.Events.DataReceived -= ServerDataReceived;
            simpleTcpServer.Stop();
            
            Assert.AreEqual(expectedClientConnectedCount, clientConnectedCount);
            Assert.IsTrue(serverReceivedData == expectedClientData, $"Server did not receive expected data");
            Assert.IsTrue(serverReceivedData.Length == expectedClientDataReceivedBytes, $"Server received: {serverReceivedData} byte(s), expected: {expectedClientDataReceivedBytes}");
            Assert.IsFalse(serverReceiveError, "Server receive error detected");
        }
    }
}