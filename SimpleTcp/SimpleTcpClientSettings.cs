using System;

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
                return _streamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("StreamBufferSize must be one or greater.");
                if (value > 65536) throw new ArgumentException("StreamBufferSize must be less than 65,536.");
                _streamBufferSize = value;
            }
        }

        /// <summary>
        /// The number of milliseconds to wait when attempting to connect.
        /// </summary>
        public int ConnectTimeoutMs
        {
            get
            {
                return _connectTimeoutMs;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ConnectTimeoutMs must be greater than zero.");
                _connectTimeoutMs = value;
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
                return _idleServerTimeoutMs;
            }
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutMs must be zero or greater.");
                _idleServerTimeoutMs = value;
            }
        }

        /// <summary>
        /// Number of milliseconds to wait between each iteration of evaluating the server connection to see if the configured timeout interval has been exceeded.
        /// </summary>
        public int IdleServerEvaluationIntervalMs
        {
            get
            {
                return _idleServerEvaluationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("IdleServerEvaluationIntervalMs must be one or greater.");
                _idleServerEvaluationIntervalMs = value;
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

        private int _streamBufferSize = 65536;
        private int _connectTimeoutMs = 5000;
        private int _idleServerTimeoutMs = 0;
        private int _idleServerEvaluationIntervalMs = 1000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SimpleTcpClientSettings()
        {

        }

        #endregion
    }
}
