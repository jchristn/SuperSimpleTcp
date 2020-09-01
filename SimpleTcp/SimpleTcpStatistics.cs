using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp statistics.
    /// </summary>
    public class SimpleTcpStatistics
    {
        #region Public-Members

        /// <summary>
        /// The time at which the client or server was started.
        /// </summary>
        public DateTime StartTime
        {
            get
            {
                return _StartTime;
            }
        }

        /// <summary>
        /// The amount of time which the client or server has been up.
        /// </summary>
        public TimeSpan UpTime
        {
            get
            {
                return DateTime.Now.ToUniversalTime() - _StartTime;
            }
        }

        /// <summary>
        /// The number of bytes received.
        /// </summary>
        public long ReceivedBytes
        {
            get
            {
                return _ReceivedBytes;
            }
            internal set
            {
                _ReceivedBytes = value;
            }
        }
         
        /// <summary>
        /// The number of bytes sent.
        /// </summary>
        public long SentBytes
        {
            get
            {
                return _SentBytes;
            }
            internal set
            {
                _SentBytes = value;
            }
        }
         
        #endregion

        #region Private-Members

        private DateTime _StartTime = DateTime.Now.ToUniversalTime();
        private long _ReceivedBytes = 0; 
        private long _SentBytes = 0; 

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the statistics object.
        /// </summary>
        public SimpleTcpStatistics()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Return human-readable version of the object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string ret =
                "--- Statistics ---" + Environment.NewLine +
                "    Started        : " + _StartTime.ToString() + Environment.NewLine +
                "    Uptime         : " + UpTime.ToString() + Environment.NewLine +
                "    Received bytes : " + ReceivedBytes + Environment.NewLine +
                "    Sent bytes     : " + SentBytes + Environment.NewLine;
            return ret;
        }

        /// <summary>
        /// Reset statistics other than StartTime and UpTime.
        /// </summary>
        public void Reset()
        {
            _ReceivedBytes = 0; 
            _SentBytes = 0; 
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
