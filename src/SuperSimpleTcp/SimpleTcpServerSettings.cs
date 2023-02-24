using System;
using System.Collections.Generic;
using System.Net.Security;

namespace SuperSimpleTcp
{
    /// <summary>
    /// SimpleTcp server settings.
    /// </summary>
    public class SimpleTcpServerSettings
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
                if (value > 65536) throw new ArgumentException("StreamBufferSize must be less than or equal to 65,536.");
                _streamBufferSize = value;
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
                return _idleClientTimeoutMs;
            }
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutMs must be zero or greater.");
                _idleClientTimeoutMs = value;
            }
        }

        /// <summary>
        /// Maximum number of connections the server will accept.
        /// Default is 4096.  Value must be greater than zero.
        /// </summary>
        public int MaxConnections
        {
            get
            {
                return _maxConnections;
            }
            set
            {
                if (value < 1) throw new ArgumentException("Max connections must be greater than zero.");
                _maxConnections = value;
            }
        }

        /// <summary>
        /// Number of milliseconds to wait between each iteration of evaluating connected clients to see if they have exceeded the configured timeout interval.
        /// </summary>
        public int IdleClientEvaluationIntervalMs
        {
            get
            {
                return _idleClientEvaluationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException("IdleClientEvaluationIntervalMs must be one or greater.");
                _idleClientEvaluationIntervalMs = value;
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

        /// <summary>
        /// The list of permitted IP addresses from which connections can be received.
        /// </summary>
        public List<string> PermittedIPs
        {
            get
            {
                return _permittedIPs;
            }
            set
            {
                if (value == null) _permittedIPs = new List<string>();
                else _permittedIPs = value;
            }
        }

        /// <summary>
        /// The list of blocked IP addresses from which connections will be declined.
        /// </summary>
        public List<string> BlockedIPs
        {
            get
            {
                return _blockedIPs;
            }
            set
            {
                if (value == null) _blockedIPs = new List<string>();
                else _blockedIPs = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _noDelay = false;
        private int _streamBufferSize = 65536;
        private int _maxConnections = 4096;
        private int _idleClientTimeoutMs = 0;
        private int _idleClientEvaluationIntervalMs = 5000;
        private List<string> _permittedIPs = new List<string>();
        private List<string> _blockedIPs = new List<string>();

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
