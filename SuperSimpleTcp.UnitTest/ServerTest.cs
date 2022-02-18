using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace SuperSimpleTcp.UnitTest
{
    [TestClass]
    public class ServerTest
    {
        //Unit Test Naming
        //[UnitOfWork_StateUnderTest_ExpectedBehavior]

        //TODO: Server hangs here at startup
        [Ignore]
        [TestMethod]
        public async Task StartAsync_ValidListenerIpAndPort_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 8000);
            await simpleTcpServer.StartAsync();
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        public void Start_ValidListenerIpAndPort_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 8001);
            simpleTcpServer.Start();
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        [DeploymentItem("simpletcp.crt")]
        public void Start_ValidListenerIpAndPortSll_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 8001, true, "simpletcp.crt", "simpletcp");
            simpleTcpServer.Start();
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        public void Start_ValidIpAndPort_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:8002");
            simpleTcpServer.Start();
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Start_ValidIpToHighPort1_ThrowException()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:65536");
            simpleTcpServer.Start();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Start_ValidIpToHighPort2_ThrowException()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:123456789");
            simpleTcpServer.Start();
        }

        [TestMethod]
        [ExpectedException(typeof(OverflowException))]
        public void Start_ValidIpToHighPort3_ThrowException()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:2147483648");
            simpleTcpServer.Start();
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void Start_CorruptPort_ThrowException()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:INVALID_PORT");
            simpleTcpServer.Start();
        }
    }
}