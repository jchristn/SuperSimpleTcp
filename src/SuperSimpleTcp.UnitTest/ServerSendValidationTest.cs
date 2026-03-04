namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.IO;
    using System.Threading.Tasks;

    [TestClass]
    public class ServerSendValidationTest
    {
        private static SimpleTcpServer CreateStartedServer()
        {
            var server = new SimpleTcpServer("127.0.0.1", 0);
            server.Start();
            return server;
        }

        #region Send(ipPort, string)

        [TestMethod]
        public void Send_String_NullIpPort_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.Send(null!, "data"));
        }

        [TestMethod]
        public void Send_String_NullData_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.Send("127.0.0.1:9999", (string)null!));
        }

        #endregion

        #region Send(ipPort, byte[])

        [TestMethod]
        public void Send_Bytes_NullIpPort_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.Send(null!, (byte[])null!));
        }

        [TestMethod]
        public void Send_Bytes_NullData_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.Send("127.0.0.1:9999", (byte[])null!));
        }

        #endregion

        #region Send(ipPort, long, Stream)

        [TestMethod]
        public void Send_Stream_NullIpPort_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.Send(null!, 10, Stream.Null));
        }

        [TestMethod]
        public void Send_Stream_ContentLengthLessThanOne_ReturnsSilently()
        {
            using var server = CreateStartedServer();
            server.Send("127.0.0.1:9999", 0, Stream.Null);
        }

        [TestMethod]
        public void Send_Stream_NullStream_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.Send("127.0.0.1:9999", 10, (Stream)null!));
        }

        #endregion

        #region SendAsync

        [TestMethod]
        public async Task SendAsync_String_NullIpPort_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.SendAsync(null!, "data"));
        }

        [TestMethod]
        public async Task SendAsync_Bytes_NullIpPort_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.SendAsync(null!, new byte[] { 1 }));
        }

        [TestMethod]
        public async Task SendAsync_Stream_NullIpPort_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => server.SendAsync(null!, 10, Stream.Null));
        }

        #endregion

        #region DisconnectClient

        [TestMethod]
        public void DisconnectClient_NullIpPort_ThrowsArgumentNullException()
        {
            using var server = CreateStartedServer();
            Assert.ThrowsExactly<ArgumentNullException>(() => server.DisconnectClient(null!));
        }

        #endregion
    }
}
