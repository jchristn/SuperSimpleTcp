namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Threading.Tasks;

    [TestClass]
    public class ServerTest
    {
        //Unit Test Naming
        //[UnitOfWork_StateUnderTest_ExpectedBehavior]

        [TestMethod]
        public async Task StartAsync_ValidListenerIpAndPort_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            _ = simpleTcpServer.StartAsync();
            await Task.Delay(100);
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        public void Start_ValidListenerIpAndPort_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            simpleTcpServer.Start();
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        public void Start_ValidListenerIpAndPortSll_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0, true, "simpletcp.pfx", "simpletcp");
            simpleTcpServer.Start();
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        public void Start_ValidIpAndPort_Successful()
        {
            using var simpleTcpServer = new SimpleTcpServer("127.0.0.1", 0);
            simpleTcpServer.Start();
            Assert.IsTrue(simpleTcpServer.IsListening);
        }

        [TestMethod]
        public void Start_ValidIpToHighPort1_ThrowException()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            {
                using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:65536");
                simpleTcpServer.Start();
            });
        }

        [TestMethod]
        public void Start_ValidIpToHighPort2_ThrowException()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            {
                using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:123456789");
                simpleTcpServer.Start();
            });
        }

        [TestMethod]
        public void Start_ValidIpToHighPort3_ThrowException()
        {
            Assert.ThrowsExactly<OverflowException>(() =>
            {
                using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:2147483648");
                simpleTcpServer.Start();
            });
        }

        [TestMethod]
        public void Start_CorruptPort_ThrowException()
        {
            Assert.ThrowsExactly<FormatException>(() =>
            {
                using var simpleTcpServer = new SimpleTcpServer("127.0.0.1:INVALID_PORT");
                simpleTcpServer.Start();
            });
        }
    }
}
