namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;

    [TestClass]
    public class ClientConstructorTest
    {
        #region string ipPort

        [TestMethod]
        public void Ctor_IpPort_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpClient((string)null!));
        }

        [TestMethod]
        public void Ctor_IpPort_Empty_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpClient(string.Empty));
        }

        [TestMethod]
        public void Ctor_IpPort_NegativePort_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new SimpleTcpClient("127.0.0.1:-1"));
        }

        [TestMethod]
        public void Ctor_IpPort_Valid_Succeeds()
        {
            using var client = new SimpleTcpClient("127.0.0.1:5000");
            Assert.IsFalse(client.IsConnected);
        }

        #endregion

        #region string hostname, int port

        [TestMethod]
        public void Ctor_HostnamePort_NullHostname_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpClient((string)null!, 5000));
        }

        [TestMethod]
        public void Ctor_HostnamePort_EmptyHostname_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpClient(string.Empty, 5000));
        }

        [TestMethod]
        public void Ctor_HostnamePort_NegativePort_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new SimpleTcpClient("127.0.0.1", -1));
        }

        [TestMethod]
        public void Ctor_HostnamePort_Valid_Succeeds()
        {
            using var client = new SimpleTcpClient("127.0.0.1", 5000);
            Assert.IsFalse(client.IsConnected);
        }

        #endregion

        #region IPEndPoint

        [TestMethod]
        public void Ctor_IPEndPoint_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpClient((IPEndPoint)null!));
        }

        [TestMethod]
        public void Ctor_IPEndPoint_Valid_Succeeds()
        {
            using var client = new SimpleTcpClient(new IPEndPoint(IPAddress.Loopback, 5000));
            Assert.IsFalse(client.IsConnected);
        }

        #endregion

        #region IPAddress, int port

        [TestMethod]
        public void Ctor_IPAddress_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => new SimpleTcpClient((IPAddress)null!, 5000));
        }

        [TestMethod]
        public void Ctor_IPAddress_Valid_Succeeds()
        {
            using var client = new SimpleTcpClient(IPAddress.Loopback, 5000);
            Assert.IsFalse(client.IsConnected);
        }

        #endregion

        #region Certificate overloads

        [TestMethod]
        public void Ctor_HostnamePort_X509Cert_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpClient("127.0.0.1", 5000, (X509Certificate2)null!));
        }

        [TestMethod]
        public void Ctor_HostnamePort_ByteCert_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpClient("127.0.0.1", 5000, (byte[])null!));
        }

        [TestMethod]
        public void Ctor_IPAddress_X509Cert_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpClient(IPAddress.Loopback, 5000, (X509Certificate2)null!));
        }

        [TestMethod]
        public void Ctor_IPAddress_ByteCert_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpClient(IPAddress.Loopback, 5000, (byte[])null!));
        }

        [TestMethod]
        public void Ctor_IPEndPoint_X509Cert_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpClient(new IPEndPoint(IPAddress.Loopback, 5000), (X509Certificate2)null!));
        }

        [TestMethod]
        public void Ctor_IPEndPoint_ByteCert_Null_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                new SimpleTcpClient(new IPEndPoint(IPAddress.Loopback, 5000), (byte[])null!));
        }

        #endregion

        #region ServerIpPort property

        [TestMethod]
        public void ServerIpPort_AfterConstruction_ReturnsCorrectFormat()
        {
            using var client = new SimpleTcpClient("127.0.0.1", 12345);
            Assert.AreEqual("127.0.0.1:12345", client.ServerIpPort);
        }

        [TestMethod]
        public void ServerIpPort_FromIpPort_ReturnsCorrectFormat()
        {
            using var client = new SimpleTcpClient("127.0.0.1:9999");
            Assert.AreEqual("127.0.0.1:9999", client.ServerIpPort);
        }

        [TestMethod]
        public void ServerIpPort_FromIPEndPoint_ReturnsCorrectFormat()
        {
            using var client = new SimpleTcpClient(new IPEndPoint(IPAddress.Loopback, 8080));
            Assert.AreEqual("127.0.0.1:8080", client.ServerIpPort);
        }

        #endregion
    }
}
