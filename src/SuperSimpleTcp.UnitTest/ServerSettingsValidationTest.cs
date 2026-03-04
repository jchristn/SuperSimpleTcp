namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;

    [TestClass]
    public class ServerSettingsValidationTest
    {
        #region StreamBufferSize

        [TestMethod]
        public void StreamBufferSize_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpServerSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.StreamBufferSize = 0);
        }

        [TestMethod]
        public void StreamBufferSize_TooLarge_ThrowsArgumentException()
        {
            var settings = new SimpleTcpServerSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.StreamBufferSize = 65537);
        }

        [TestMethod]
        public void StreamBufferSize_Max_Succeeds()
        {
            var settings = new SimpleTcpServerSettings();
            settings.StreamBufferSize = 65536;
            Assert.AreEqual(65536, settings.StreamBufferSize);
        }

        #endregion

        #region MaxConnections

        [TestMethod]
        public void MaxConnections_Zero_ThrowsArgumentException()
        {
            var settings = new SimpleTcpServerSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.MaxConnections = 0);
        }

        [TestMethod]
        public void MaxConnections_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpServerSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.MaxConnections = -1);
        }

        [TestMethod]
        public void MaxConnections_Valid_Succeeds()
        {
            var settings = new SimpleTcpServerSettings();
            settings.MaxConnections = 100;
            Assert.AreEqual(100, settings.MaxConnections);
        }

        #endregion

        #region IdleClientTimeoutMs

        [TestMethod]
        public void IdleClientTimeoutMs_Negative_ThrowsArgumentException()
        {
            var settings = new SimpleTcpServerSettings();
            Assert.ThrowsExactly<ArgumentException>(() => settings.IdleClientTimeoutMs = -1);
        }

        [TestMethod]
        public void IdleClientTimeoutMs_Zero_Succeeds()
        {
            var settings = new SimpleTcpServerSettings();
            settings.IdleClientTimeoutMs = 0;
            Assert.AreEqual(0, settings.IdleClientTimeoutMs);
        }

        #endregion

        #region IdleClientEvaluationIntervalMs

        [TestMethod]
        public void IdleClientEvaluationIntervalMs_Zero_ThrowsArgumentOutOfRangeException()
        {
            var settings = new SimpleTcpServerSettings();
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.IdleClientEvaluationIntervalMs = 0);
        }

        [TestMethod]
        public void IdleClientEvaluationIntervalMs_Valid_Succeeds()
        {
            var settings = new SimpleTcpServerSettings();
            settings.IdleClientEvaluationIntervalMs = 3000;
            Assert.AreEqual(3000, settings.IdleClientEvaluationIntervalMs);
        }

        #endregion

        #region PermittedIPs and BlockedIPs

        [TestMethod]
        public void PermittedIPs_SetNull_InitializesEmptyList()
        {
            var settings = new SimpleTcpServerSettings();
            settings.PermittedIPs = null;
            Assert.IsNotNull(settings.PermittedIPs);
            Assert.AreEqual(0, settings.PermittedIPs.Count);
        }

        [TestMethod]
        public void PermittedIPs_SetValidList_SetsCorrectly()
        {
            var settings = new SimpleTcpServerSettings();
            var list = new List<string> { "192.168.1.1", "10.0.0.1" };
            settings.PermittedIPs = list;
            Assert.AreEqual(2, settings.PermittedIPs.Count);
        }

        [TestMethod]
        public void BlockedIPs_SetNull_InitializesEmptyList()
        {
            var settings = new SimpleTcpServerSettings();
            settings.BlockedIPs = null;
            Assert.IsNotNull(settings.BlockedIPs);
            Assert.AreEqual(0, settings.BlockedIPs.Count);
        }

        [TestMethod]
        public void BlockedIPs_SetValidList_SetsCorrectly()
        {
            var settings = new SimpleTcpServerSettings();
            var list = new List<string> { "192.168.1.100" };
            settings.BlockedIPs = list;
            Assert.AreEqual(1, settings.BlockedIPs.Count);
        }

        #endregion
    }
}
