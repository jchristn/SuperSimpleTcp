using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Threading.Tasks;

namespace SuperSimpleTcp.UnitTest
{
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
            var port = 8000;
            var testData = StringHelper.RandomString(1000);
            var receiveError = false;

            var expectedClientConnectedCount = 1;
            var clientConnectedCount = 0;

            void ClientConnected(object? sender, ConnectionEventArgs e)
            {
                clientConnectedCount++;
            }

            void DataReceived(object? sender, DataReceivedEventArgs e)
            {
                var receivedTestData = Encoding.UTF8.GetString(e.Data);
                if (testData != receivedTestData)
                {
                    receiveError = true;
                }
            }

            using var simpleTcpServer = new SimpleTcpServer($"{ipAddress}:{port}");
            simpleTcpServer.Start();
            simpleTcpServer.Events.ClientConnected += ClientConnected;
            simpleTcpServer.Events.DataReceived += DataReceived;

            using var simpleTcpClient = new SimpleTcpClient($"{ipAddress}:{port}");
            simpleTcpClient.Connect();
            for (var i = 0; i < 20; i++)
            {
                simpleTcpClient.Send(testData);
                await Task.Delay(100);
            }
            simpleTcpClient.Disconnect();

            await Task.Delay(10);

            simpleTcpServer.Events.ClientConnected -= ClientConnected;
            simpleTcpServer.Events.DataReceived -= DataReceived;
            simpleTcpServer.Stop();

            Assert.AreEqual(expectedClientConnectedCount, clientConnectedCount);
            Assert.IsFalse(receiveError, "Receive errors detected");
        }
    }
}