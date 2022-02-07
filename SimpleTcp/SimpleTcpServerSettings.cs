using System;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp server settings.
    /// </summary>
    public class SimpleTcpServerSettings
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
                if (value > 65536) throw new ArgumentException("StreamBufferSize must be less than or equal to 65,536.");
                _StreamBufferSize = value;
            }
        }

        /// <summary>
        /// Maximum amount of time to wait before considering a client idle and disconnecting them. 
        /// By default, this value is set to 0, which will never disconnect a client due to inactivity.
        /// The timeout is reset any time a message is received from a client.
        /// For instance, if you set this value to 30000, the client will be disconnected if the server has not received a message from the client within 30 seconds.
        /// </summary>
        public int IdleClientTimeoutMs
        {
            get
            {
                return _IdleClientTimeoutMs;
            }
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutMs must be zero or greater.");
                _IdleClientTimeoutMs = value;
            }
        }

        /// <summary>
        /// Number of milliseconds to wait between each iteration of evaluating connected clients to see if they have exceeded the configured timeout interval.
        /// </summary>
        public int IdleClientEvaluationIntervalMs
        {
            get
            {
                return _IdleClientEvaluationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("IdleClientEvaluationIntervalMs must be one or greater.");
                _IdleClientEvaluationIntervalMs = value;
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
        private int _IdleClientTimeoutMs = 0;
        private int _IdleClientEvaluationIntervalMs = 5000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SimpleTcpServerSettings()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
