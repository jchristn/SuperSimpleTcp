using System;
using System.Net.Security;

namespace SuperSimpleTcp
{
    /// <summary>
    /// SimpleTcp client settings.
    /// </summary>
    public class SimpleTcpClientSettings
    {
        #region Public-Members

        /// <summary>
        /// Gets or sets a value that disables a delay when send or receive buffers are not full.
        /// true if the delay is disabled; otherwise, false. The default value is false.
        /// </summary>
        public bool NoDelay
        {
            get
            {
                return _noDelay;
            }
            set
            {
                _noDelay = value;
            }
        }

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
        /// The number of milliseconds to wait when attempting to read before returning null.
        /// </summary>
        public int ReadTimeoutMs
        {
            get
            {
                return _readTimeoutMs;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ReadTimeoutMs must be greater than zero.");
                _readTimeoutMs = value;
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
        /// Number of milliseconds to wait between each iteration of evaluating the server connection to see if the connection is lost.
        /// </summary>
        public int ConnectionLostEvaluationIntervalMs
        {
            get
            {
                return _connectionLostEvaluationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("ConnectionLostEvaluationIntervalMs must be one or greater.");
                _connectionLostEvaluationIntervalMs = value;
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

        /// <summary>
        /// Enable or disable whether the data receiver thread fires the DataReceived event from a background task.
        /// The default is enabled.
        /// </summary>
        public bool UseAsyncDataReceivedEvents = true;

        /// <summary>
        /// Enable or disable checking certificate revocation list during the validation process.
        /// </summary>
        public bool CheckCertificateRevocation = true;

        /// <summary>
        /// Delegate responsible for validating a certificate supplied by a remote party.
        /// </summary>
        public RemoteCertificateValidationCallback CertificateValidationCallback = null;

        #endregion

        #region Private-Members

        private bool _noDelay = false;
        private int _streamBufferSize = 65536;
        private int _connectTimeoutMs = 5000;
        private int _readTimeoutMs = 1000;
        private int _idleServerTimeoutMs = 0;
        private int _idleServerEvaluationIntervalMs = 1000;
        private int _connectionLostEvaluationIntervalMs = 200;

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
