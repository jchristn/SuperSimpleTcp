using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp client settings.
    /// </summary>
    public class SimpleTcpClientSettings
    {
        #region Public-Members

        /// <summary>
        /// Buffer size to use while interacting with streams. 
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("StreamBufferSize must be one or greater.");
                if (value > 65536) throw new ArgumentException("StreamBufferSize must be less than 65,536.");
                _StreamBufferSize = value;
            }
        }

        /// <summary>
        /// The number of milliseconds to wait when attempting to connect.
        /// </summary>
        public int ConnectTimeoutMs
        {
            get
            {
                return _ConnectTimeoutMs;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ConnectTimeoutMs must be greater than zero.");
                _ConnectTimeoutMs = value;
            }
        }

        /// <summary>
        /// Maximum amount of time to wait before considering the server to be idle and disconnecting from it. 
        /// By default, this value is set to 0, which will never disconnect due to inactivity.
        /// The timeout is reset any time a message is received from the server.
        /// For instance, if you set this value to 30000, the client will disconnect if the server has not sent a message to the client within 30 seconds.
        /// </summary>
        public int IdleServerTimeoutMs
        {
            get
            {
                return _IdleServerTimeoutMs;
            }
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutMs must be zero or greater.");
                _IdleServerTimeoutMs = value;
            }
        }

        /// <summary>
        /// Number of milliseconds to wait between each iteration of evaluating the server connection to see if the configured timeout interval has been exceeded.
        /// </summary>
        public int IdleServerEvaluationIntervalMs
        {
            get
            {
                return _IdleServerEvaluationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("IdleServerEvaluationIntervalMs must be one or greater.");
                _IdleServerEvaluationIntervalMs = value;
            }
        }

        /// <summary>
        /// Enable or disable acceptance of invalid SSL certificates.
        /// </summary>
        public bool AcceptInvalidCertificates = true;

        /// <summary>
        /// Enable or disable mutual authentication of SSL client and server.
        /// </summary>
        public bool MutuallyAuthenticate = true;

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private int _ConnectTimeoutMs = 5000;
        private int _IdleServerTimeoutMs = 0;
        private int _IdleServerEvaluationIntervalMs = 1000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SimpleTcpClientSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
