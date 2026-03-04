namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class KeepaliveSettingsTest
    {
        #region TcpKeepAliveInterval

        [TestMethod]
        public void TcpKeepAliveInterval_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.TcpKeepAliveInterval = 0);
        }

        [TestMethod]
        public void TcpKeepAliveInterval_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.TcpKeepAliveInterval = -1);
        }

        [TestMethod]
        public void TcpKeepAliveInterval_Valid_Succeeds()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            settings.TcpKeepAliveInterval = 10;
            Assert.AreEqual(10, settings.TcpKeepAliveInterval);
        }

        #endregion

        #region TcpKeepAliveTime

        [TestMethod]
        public void TcpKeepAliveTime_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.TcpKeepAliveTime = 0);
        }

        [TestMethod]
        public void TcpKeepAliveTime_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.TcpKeepAliveTime = -1);
        }

        [TestMethod]
        public void TcpKeepAliveTime_Valid_Succeeds()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            settings.TcpKeepAliveTime = 15;
            Assert.AreEqual(15, settings.TcpKeepAliveTime);
        }

        #endregion

        #region TcpKeepAliveRetryCount

        [TestMethod]
        public void TcpKeepAliveRetryCount_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.TcpKeepAliveRetryCount = 0);
        }

        [TestMethod]
        public void TcpKeepAliveRetryCount_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.TcpKeepAliveRetryCount = -1);
        }

        [TestMethod]
        public void TcpKeepAliveRetryCount_Valid_Succeeds()
        {
            var settings = new SimpleTcpKeepaliveSettings();
            settings.TcpKeepAliveRetryCount = 5;
            Assert.AreEqual(5, settings.TcpKeepAliveRetryCount);
        }

        #endregion
    }
}
