namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class ServerConstructorTest
    {
        #region string ipPort

        [TestMethod]
        public void Ctor_IpPort_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpServer((string)null!));
        }

        [TestMethod]
        public void Ctor_IpPort_Empty_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpServer(string.Empty));
        }

        [TestMethod]
        public void Ctor_IpPort_Valid_IsListeningFalse()
        {
            using var server = new SimpleTcpServer("127.0.0.1:10000");
            Assert.IsFalse(server.IsListening);
        }

        #endregion

        #region string listenerIp, int port

        [TestMethod]
        public void Ctor_ListenerIpPort_NegativePort_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new SimpleTcpServer("127.0.0.1", -1));
        }

        [TestMethod]
        public void Ctor_ListenerIpPort_NullIp_DefaultsToLoopback()
        {
            using var server = new SimpleTcpServer(null!, 10001);
            Assert.IsFalse(server.IsListening);
        }

        [TestMethod]
        public void Ctor_ListenerIpPort_Wildcard_Succeeds()
        {
            using var server = new SimpleTcpServer("*", 10002);
            Assert.IsFalse(server.IsListening);
        }

        [TestMethod]
        public void Ctor_ListenerIpPort_Valid_IsListeningFalse()
        {
            using var server = new SimpleTcpServer("127.0.0.1", 10003);
            Assert.IsFalse(server.IsListening);
        }

        #endregion

        #region string listenerIp, int port, bool ssl, string, string

        [TestMethod]
        public void Ctor_ListenerIpPortSsl_NegativePort_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new SimpleTcpServer("127.0.0.1", -1, false, null!, null!));
        }

        #endregion

        #region string listenerIp, int port, byte[] certificate

        [TestMethod]
        public void Ctor_ListenerIpPortCert_NullCert_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpServer("127.0.0.1", 10004, (byte[])null!));
        }

        [TestMethod]
        public void Ctor_ListenerIpPortCert_NegativePort_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                new SimpleTcpServer("127.0.0.1", -1, new byte[] { 1 }));
        }

        #endregion

        #region string ipPort, bool ssl, string, string

        [TestMethod]
        public void Ctor_IpPortSsl_NullIpPort_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpServer((string)null!, false, null!, null!));
        }

        #endregion
    }
}
