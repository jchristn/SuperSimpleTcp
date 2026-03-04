namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.IO;
    using System.Threading.Tasks;

    [TestClass]
    public class ClientSendValidationTest
    {
        #region Send(string)

        [TestMethod]
        public void Send_String_Null_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10100");
            Assert.ThrowsExactly<ArgumentNullException>(() => client.Send((string)null!));
        }

        [TestMethod]
        public void Send_String_Empty_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10101");
            Assert.ThrowsExactly<ArgumentNullException>(() => client.Send(string.Empty));
        }

        [TestMethod]
        public void Send_String_NotConnected_ThrowsIOException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10102");
            Assert.ThrowsExactly<IOException>(() => client.Send("hello"));
        }

        #endregion

        #region Send(byte[])

        [TestMethod]
        public void Send_Bytes_Null_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10103");
            Assert.ThrowsExactly<ArgumentNullException>(() => client.Send((byte[])null!));
        }

        [TestMethod]
        public void Send_Bytes_Empty_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10104");
            Assert.ThrowsExactly<ArgumentNullException>(() => client.Send(new byte[0]));
        }

        #endregion

        #region Send(long, Stream)

        [TestMethod]
        public void Send_Stream_ContentLengthLessThanOne_ReturnsSilently()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10105");
            client.Send(0, Stream.Null);
        }

        [TestMethod]
        public void Send_Stream_NullStream_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10106");
            Assert.ThrowsExactly<ArgumentNullException>(() => client.Send(10, (Stream)null!));
        }

        #endregion

        #region SendAsync(string)

        [TestMethod]
        public async Task SendAsync_String_Null_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10107");
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendAsync((string)null!));
        }

        [TestMethod]
        public async Task SendAsync_String_NotConnected_ThrowsIOException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10108");
            await Assert.ThrowsExactlyAsync<IOException>(() => client.SendAsync("hello"));
        }

        #endregion

        #region SendAsync(byte[])

        [TestMethod]
        public async Task SendAsync_Bytes_Null_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10109");
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendAsync((byte[])null!));
        }

        #endregion

        #region SendAsync(long, Stream)

        [TestMethod]
        public async Task SendAsync_Stream_ContentLengthLessThanOne_ReturnsSilently()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10110");
            await client.SendAsync(0, Stream.Null);
        }

        [TestMethod]
        public async Task SendAsync_Stream_NullStream_ThrowsArgumentNullException()
        {
            using var client = new SimpleTcpClient("127.0.0.1:10111");
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => client.SendAsync(10, (Stream)null!));
        }

        #endregion
    }
}
