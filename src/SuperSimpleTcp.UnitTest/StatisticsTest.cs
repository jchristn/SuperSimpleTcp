namespace SuperSimpleTcp.UnitTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class StatisticsTest
    {
        [TestMethod]
        public void StartTime_IsRecent()
        {
            var before = DateTime.Now.ToUniversalTime();
            var stats = new SimpleTcpStatistics();
            var after = DateTime.Now.ToUniversalTime();

            Assert.IsTrue(stats.StartTime >= before.AddSeconds(-1));
            Assert.IsTrue(stats.StartTime <= after.AddSeconds(1));
        }

        [TestMethod]
        public void UpTime_IsNonNegative()
        {
            var stats = new SimpleTcpStatistics();
            Assert.IsTrue(stats.UpTime.TotalMilliseconds >= 0);
        }

        [TestMethod]
        public void SentAndReceivedBytes_StartAtZero()
        {
            var stats = new SimpleTcpStatistics();
            Assert.AreEqual(0, stats.SentBytes);
            Assert.AreEqual(0, stats.ReceivedBytes);
        }

        [TestMethod]
        public void Reset_RunsWithoutError()
        {
            var stats = new SimpleTcpStatistics();
            stats.Reset();
            Assert.AreEqual(0, stats.SentBytes);
            Assert.AreEqual(0, stats.ReceivedBytes);
        }

        [TestMethod]
        public void ToString_ContainsExpectedLabels()
        {
            var stats = new SimpleTcpStatistics();
            var result = stats.ToString();

            Assert.IsTrue(result.Contains("Statistics"));
            Assert.IsTrue(result.Contains("Started"));
            Assert.IsTrue(result.Contains("Uptime"));
            Assert.IsTrue(result.Contains("Received bytes"));
            Assert.IsTrue(result.Contains("Sent bytes"));
        }
    }
}
