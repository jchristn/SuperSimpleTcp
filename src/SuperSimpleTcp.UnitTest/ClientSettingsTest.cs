namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class ClientSettingsTest
    {
        #region StreamBufferSize

        [TestMethod]
        public void StreamBufferSize_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.StreamBufferSize = 0);
        }

        [TestMethod]
        public void StreamBufferSize_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.StreamBufferSize = -1);
        }

        [TestMethod]
        public void StreamBufferSize_TooLarge_ThrowsArgumentException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.StreamBufferSize = 65537);
        }

        [TestMethod]
        public void StreamBufferSize_One_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.StreamBufferSize = 1;
            Assert.AreEqual(1, settings.StreamBufferSize);
        }

        [TestMethod]
        public void StreamBufferSize_1024_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.StreamBufferSize = 1024;
            Assert.AreEqual(1024, settings.StreamBufferSize);
        }

        [TestMethod]
        public void StreamBufferSize_Max_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.StreamBufferSize = 65536;
            Assert.AreEqual(65536, settings.StreamBufferSize);
        }

        #endregion

        #region ConnectTimeoutMs

        [TestMethod]
        public void ConnectTimeoutMs_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.ConnectTimeoutMs = 0);
        }

        [TestMethod]
        public void ConnectTimeoutMs_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.ConnectTimeoutMs = -1);
        }

        [TestMethod]
        public void ConnectTimeoutMs_Valid_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.ConnectTimeoutMs = 3000;
            Assert.AreEqual(3000, settings.ConnectTimeoutMs);
        }

        #endregion

        #region ReadTimeoutMs

        [TestMethod]
        public void ReadTimeoutMs_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.ReadTimeoutMs = 0);
        }

        [TestMethod]
        public void ReadTimeoutMs_Valid_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.ReadTimeoutMs = 500;
            Assert.AreEqual(500, settings.ReadTimeoutMs);
        }

        #endregion

        #region IdleServerTimeoutMs

        [TestMethod]
        public void IdleServerTimeoutMs_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.IdleServerTimeoutMs = -1);
        }

        [TestMethod]
        public void IdleServerTimeoutMs_Zero_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.IdleServerTimeoutMs = 0;
            Assert.AreEqual(0, settings.IdleServerTimeoutMs);
        }

        [TestMethod]
        public void IdleServerTimeoutMs_Valid_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.IdleServerTimeoutMs = 30000;
            Assert.AreEqual(30000, settings.IdleServerTimeoutMs);
        }

        #endregion

        #region IdleServerEvaluationIntervalMs

        [TestMethod]
        public void IdleServerEvaluationIntervalMs_Zero_ThrowsArgumentOutOfRangeException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.IdleServerEvaluationIntervalMs = 0);
        }

        [TestMethod]
        public void IdleServerEvaluationIntervalMs_Valid_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.IdleServerEvaluationIntervalMs = 2000;
            Assert.AreEqual(2000, settings.IdleServerEvaluationIntervalMs);
        }

        #endregion

        #region ConnectionLostEvaluationIntervalMs

        [TestMethod]
        public void ConnectionLostEvaluationIntervalMs_Zero_ThrowsArgumentOutOfRangeException()
        {
            var settings = new SimpleTcpClientSettings();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.ConnectionLostEvaluationIntervalMs = 0);
        }

        [TestMethod]
        public void ConnectionLostEvaluationIntervalMs_Valid_Succeeds()
        {
            var settings = new SimpleTcpClientSettings();
            settings.ConnectionLostEvaluationIntervalMs = 500;
            Assert.AreEqual(500, settings.ConnectionLostEvaluationIntervalMs);
        }

        #endregion
    }
}
