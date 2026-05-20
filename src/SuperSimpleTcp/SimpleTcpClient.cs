namespace SuperSimpleTcp
{
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    using System.Buffers;
#endif
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// SimpleTcp client with SSL support.  
    /// Set the Connected, Disconnected, and DataReceived events.  
    /// Once set, use Connect() to connect to the server.
    /// </summary>
    public class SimpleTcpClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether or not the client is connected to the server.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
            private set
            {
                _isConnected = value;
            }
        }

        /// <summary>
        /// Client IPEndPoint if connected.
        /// </summary>
        public IPEndPoint LocalEndpoint
        {
            get
            {
                if (_client != null && _isConnected)
                {
                    return (IPEndPoint)_client.Client.LocalEndPoint;
                }

                return null;
            }
        }

        /// <summary>
        /// SimpleTcp client settings.
        /// </summary>
        public SimpleTcpClientSettings Settings
        {
            get
            {
                return _settings;
            }
            set
            {
                if (value == null) _settings = new SimpleTcpClientSettings();
                else _settings = value;
            }
        }

        /// <summary>
        /// SimpleTcp client events.
        /// </summary>
        public SimpleTcpClientEvents Events
        {
            get
            {
                return _events;
            }
            set
            {
                if (value == null) _events = new SimpleTcpClientEvents();
                else _events = value;
            }
        }

        /// <summary>
        /// SimpleTcp statistics.
        /// </summary>
        public SimpleTcpStatistics Statistics
        {
            get
            {
                return _statistics;
            }
        }

        /// <summary>
        /// SimpleTcp keepalive settings.
        /// </summary>
        public SimpleTcpKeepaliveSettings Keepalive
        {
            get
            {
                return _keepalive;
            }
            set
            {
                if (value == null) _keepalive = new SimpleTcpKeepaliveSettings();
                else _keepalive = value;
            }
        }

        /// <summary>
        /// Method to invoke to send a log message.
        /// </summary>
        public Action<string> Logger = null;

        /// <summary>
        /// The IP:port of the server to which this client is mapped.
        /// </summary>
        public string ServerIpPort
        {
            get
            {
                return $"{_serverIp}:{_serverPort}";
            }
        }

        #endregion

        #region Private-Members

        private readonly string _header = "[SimpleTcp.Client] ";
        private SimpleTcpClientSettings _settings = new SimpleTcpClientSettings();
        private SimpleTcpClientEvents _events = new SimpleTcpClientEvents();
        private SimpleTcpKeepaliveSettings _keepalive = new SimpleTcpKeepaliveSettings();
        private SimpleTcpStatistics _statistics = new SimpleTcpStatistics();

        private string _serverIp = null;
        private AddressFamily _addressFamily = AddressFamily.InterNetwork;
        private int _serverPort = 0;
        private readonly IPAddress _ipAddress = null;
        private TcpClient _client = null;
        private NetworkStream _networkStream = null;

        private bool _ssl = false;
        private string _pfxCertFilename = null;
        private string _pfxPassword = null;
        private SslStream _sslStream = null;
        private X509Certificate2 _sslCert = null;
        private X509Certificate2Collection _sslCertCollection = null;

        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private bool _isConnected = false;

        private Task _dataReceiver = null;
        private Task _idleServerMonitor = null;
        private Task _connectionMonitor = null;
        private AsyncEventDispatcher<DataReceivedEventArgs> _asyncDataReceivedDispatcher = null;
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private CancellationToken _token;
        private SemaphoreSlim _connectMutex = new SemaphoreSlim(1, 1);

        private long _lastActivityTimestamp = MonotonicTime.GetTimestamp();
        private bool _isTimeout = false;
        private readonly byte[] _pollBuffer = new byte[1];

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP client without SSL. 
        /// Set the Connected, Disconnected, and DataReceived callbacks. Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        public SimpleTcpClient(string ipPort)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _serverIp, out _serverPort);
            if (_serverPort < 0) throw new ArgumentException("Port must be zero or greater.");
            if (string.IsNullOrEmpty(_serverIp)) throw new ArgumentNullException("Server IP or hostname must not be null.");

            if (!IPAddress.TryParse(_serverIp, out _ipAddress))
            {
                _ipAddress = Dns.GetHostEntry(_serverIp).AddressList[0];
                _serverIp = _ipAddress.ToString();
                _addressFamily = _ipAddress.AddressFamily;
            }
            else
            {
                _addressFamily = _ipAddress.AddressFamily;
            }
        }

        /// <summary>
        /// Instantiates the TCP client. 
        /// Set the Connected, Disconnected, and DataReceived callbacks. Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(
            string ipPort,
            bool ssl,
            string pfxCertFilename,
            string pfxPassword) : this(ipPort)
        {
            _ssl = ssl;
            _pfxCertFilename = pfxCertFilename;
            _pfxPassword = pfxPassword;
        }

        /// <summary>
        /// Instantiates the TCP client without SSL. 
        /// Set the Connected, Disconnected, and DataReceived callbacks. Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        public SimpleTcpClient(string serverIpOrHostname, int port)
        {
            if (string.IsNullOrEmpty(serverIpOrHostname)) throw new ArgumentNullException(nameof(serverIpOrHostname));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _serverIp = serverIpOrHostname;
            _serverPort = port;

            if (!IPAddress.TryParse(_serverIp, out _ipAddress))
            {
                _ipAddress = Dns.GetHostEntry(serverIpOrHostname).AddressList[0];
                _serverIp = _ipAddress.ToString();
                _addressFamily = _ipAddress.AddressFamily;
            }
            else
            {
                _addressFamily = _ipAddress.AddressFamily;
            }
        }

        /// <summary>
        /// Instantiates the TCP client.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(
            string serverIpOrHostname,
            int port,
            bool ssl,
            string pfxCertFilename,
            string pfxPassword) : this(serverIpOrHostname, port)
        {
            _ssl = ssl;
            _pfxCertFilename = pfxCertFilename;
            _pfxPassword = pfxPassword;
        }

        /// <summary>
        /// Instantiates the TCP client with SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="certificate">Certificate.</param>
        public SimpleTcpClient(
            string serverIpOrHostname,
            int port,
            X509Certificate2 certificate) : this(serverIpOrHostname, port)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            _ssl = true;
            _sslCert = certificate;
        }

        /// <summary>
        /// Instantiates the TCP client with SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="certificate">Byte array containing the certificate.</param>
        public SimpleTcpClient(
            string serverIpOrHostname,
            int port,
            byte[] certificate) : this(serverIpOrHostname, port)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            _ssl = true;
#if NET9_0_OR_GREATER
            _sslCert = X509CertificateLoader.LoadPkcs12(certificate, null);
#else
            _sslCert = new X509Certificate2(certificate);
#endif
        }

        /// <summary>
        /// Instantiates the TCP client without SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpAddress">The server IP address.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        public SimpleTcpClient(IPAddress serverIpAddress, int port) : this(new IPEndPoint(serverIpAddress, port))
        {
        }

        /// <summary>
        /// Instantiates the TCP client.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpAddress">The server IP address.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(
            IPAddress serverIpAddress,
            int port,
            bool ssl,
            string pfxCertFilename,
            string pfxPassword) : this(serverIpAddress, port)
        {
            _ssl = ssl;
            _pfxCertFilename = pfxCertFilename;
            _pfxPassword = pfxPassword;
        }

        /// <summary>
        /// Instantiates the TCP client with SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpAddress">The server IP address.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="certificate">Certificate.</param>
        public SimpleTcpClient(
            IPAddress serverIpAddress,
            int port,
            X509Certificate2 certificate) : this(serverIpAddress, port)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            _ssl = true;
            _sslCert = certificate;
        }

        /// <summary>
        /// Instantiates the TCP client with SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpAddress">The server IP address.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="certificate">Byte array containing the certificate.</param>
        public SimpleTcpClient(
            IPAddress serverIpAddress,
            int port,
            byte[] certificate) : this(serverIpAddress, port)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            _ssl = true;
#if NET9_0_OR_GREATER
            _sslCert = X509CertificateLoader.LoadPkcs12(certificate, null);
#else
            _sslCert = new X509Certificate2(certificate);
#endif
            _sslCertCollection = new X509Certificate2Collection { _sslCert };
        }

        /// <summary>
        /// Instantiates the TCP client without SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpEndPoint">The server IP endpoint.</param>
        public SimpleTcpClient(IPEndPoint serverIpEndPoint)
        {
            if (serverIpEndPoint == null) throw new ArgumentNullException(nameof(serverIpEndPoint));
            else if (serverIpEndPoint.Port < 0) throw new ArgumentException("Port must be zero or greater.");
            else
            {
                _ipAddress = serverIpEndPoint.Address;
                _serverIp = serverIpEndPoint.Address.ToString();
                _serverPort = serverIpEndPoint.Port;
                _addressFamily = serverIpEndPoint.AddressFamily;
            }
        }

        /// <summary>
        /// Instantiates the TCP client.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpEndPoint">The server IP endpoint.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(IPEndPoint serverIpEndPoint, bool ssl, string pfxCertFilename, string pfxPassword) : this(serverIpEndPoint)
        {
            _ssl = ssl;
            _pfxCertFilename = pfxCertFilename;
            _pfxPassword = pfxPassword;
        }

        /// <summary>
        /// Instantiates the TCP client with SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpEndPoint">The server IP endpoint.</param>
        /// <param name="certificate">Certificate.</param>
        public SimpleTcpClient(IPEndPoint serverIpEndPoint, X509Certificate2 certificate) : this(serverIpEndPoint)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            _ssl = true;
            _sslCert = certificate;
        }

        /// <summary>
        /// Instantiates the TCP client with SSL.  
        /// Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpEndPoint">The server IP endpoint.</param>
        /// <param name="certificate">Byte array containing the certificate.</param>
        public SimpleTcpClient(IPEndPoint serverIpEndPoint, byte[] certificate) : this(serverIpEndPoint)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            _ssl = true;
#if NET9_0_OR_GREATER
            _sslCert = X509CertificateLoader.LoadPkcs12(certificate, null);
#else
            _sslCert = new X509Certificate2(certificate);
#endif
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of the TCP client.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Establish a connection to the server.
        /// </summary>
        public void Connect()
        {
            if (IsConnected)
            {
                Logger?.Invoke($"{_header}already connected");
                return;
            }

            _connectMutex.Wait();
            try
            {
                if (IsConnected)
                {
                    Logger?.Invoke($"{_header}already connected");
                    return;
                }

                BeginConnect();
                ConnectCore(_settings.ConnectTimeoutMs);
                EndConnect();
            }
            finally
            {
                _connectMutex.Release();
            }
        }

        /// <summary>
        /// Establish a connection to the server asynchronously.
        /// </summary>
        public async Task ConnectAsync(CancellationToken token = default)
        {
            if (IsConnected)
            {
                Logger?.Invoke($"{_header}already connected");
                return;
            }

            token.ThrowIfCancellationRequested();

            await _connectMutex.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (IsConnected)
                {
                    Logger?.Invoke($"{_header}already connected");
                    return;
                }

                BeginConnect();
                await ConnectCoreAsync(_settings.ConnectTimeoutMs, token).ConfigureAwait(false);
                await EndConnectAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _connectMutex.Release();
            }
        }

        private void BeginConnect()
        {
            if (_tokenSource != null)
            {
                try
                {
                    if (!_tokenSource.IsCancellationRequested)
                    {
                        _tokenSource.Cancel();
                    }
                }
                catch
                {
                }

                _tokenSource.Dispose();
            }

            Logger?.Invoke($"{_header}initializing client");
            InitializeClient(_ssl, _pfxCertFilename, _pfxPassword, _sslCert);
            Logger?.Invoke($"{_header}connecting to {ServerIpPort}");

            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;
            _token.Register(() =>
            {
                try
                {
                    _sslStream?.Close();
                }
                catch
                {
                }

                try
                {
                    _networkStream?.Close();
                }
                catch
                {
                }

                try
                {
                    _client?.Close();
                }
                catch
                {
                }
            });

            if (!String.IsNullOrEmpty(_pfxCertFilename))
            {
#if NET9_0_OR_GREATER
                if (String.IsNullOrEmpty(_pfxPassword)) _sslCert = X509CertificateLoader.LoadPkcs12FromFile(_pfxCertFilename, null);
                else _sslCert = X509CertificateLoader.LoadPkcs12FromFile(_pfxCertFilename, _pfxPassword);
#else
                if (String.IsNullOrEmpty(_pfxPassword)) _sslCert = new X509Certificate2(_pfxCertFilename);
                else _sslCert = new X509Certificate2(_pfxCertFilename, _pfxPassword);
#endif
                _sslCertCollection = new X509Certificate2Collection { _sslCert };
            }
        }

        private void ConnectCore(int timeoutMs)
        {
            var ar = _client.BeginConnect(_serverIp, _serverPort, null, null);
            using (var wh = ar.AsyncWaitHandle)
            {
                if (!wh.WaitOne(TimeSpan.FromMilliseconds(timeoutMs), false))
                {
                    _client.Close();
                    throw new TimeoutException($"Timeout connecting to {ServerIpPort}");
                }
                _client.EndConnect(ar);
            }
        }

        private async Task ConnectCoreAsync(int timeoutMs, CancellationToken token)
        {
            var connectTask = Task.Factory.FromAsync(_client.BeginConnect, _client.EndConnect, _serverIp, _serverPort, null);
            var timeoutOrCancelTask = Task.Delay(timeoutMs, token);

            var completed = await Task.WhenAny(connectTask, timeoutOrCancelTask).ConfigureAwait(false);

            if (completed == connectTask)
            {
                // Propagate connect failures
                await connectTask.ConfigureAwait(false);

                // If cancellation arrived after connect completed, close and honor cancellation.
                if (token.IsCancellationRequested)
                {
                    _client.Close();
                    token.ThrowIfCancellationRequested();
                }

                return;
            }

            // Timeout OR cancellation won the race. Abort pending connect by closing the socket.
            _client.Close();

            // Ensure EndConnect exception is always observed (prevents unobserved-fault issues).
            // Use ContinueWith so the exception is observed even if the task faults asynchronously
            // after Close() triggers the socket teardown.
            _ = connectTask.ContinueWith(
                t => { _ = t.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            token.ThrowIfCancellationRequested();
            throw new TimeoutException($"Timeout connecting to {ServerIpPort}");
        }

        private void EndConnect()
        {
            InitializeConnectedStreams();

            if (_ssl)
            {
                _sslStream.AuthenticateAsClient(_serverIp, _sslCertCollection, SslProtocols.Tls12, _settings.CheckCertificateRevocation);
                ValidateAuthenticatedStream();
            }

            CompleteConnect();
        }

        private async Task EndConnectAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            InitializeConnectedStreams();

            if (_ssl)
            {
                await _sslStream
                    .AuthenticateAsClientAsync(_serverIp, _sslCertCollection, SslProtocols.Tls12, _settings.CheckCertificateRevocation)
                    .ConfigureAwait(false);

                token.ThrowIfCancellationRequested();
                ValidateAuthenticatedStream();
            }

            CompleteConnect();
        }

        /// <summary>
        /// Establish the connection to the server with retries up to either the timeout specified or the value in Settings.ConnectTimeoutMs.
        /// </summary>
        /// <param name="timeoutMs">The amount of time in milliseconds to continue attempting connections.</param>
        public void ConnectWithRetries(int? timeoutMs = null)
        {
            if (timeoutMs != null && timeoutMs < 1) throw new ArgumentException("Timeout milliseconds must be greater than zero.");
            if (timeoutMs != null) _settings.ConnectTimeoutMs = timeoutMs.Value;

            if (IsConnected)
            {
                Logger?.Invoke($"{_header}already connected");
                return;
            }

            _connectMutex.Wait();
            try
            {
                if (IsConnected)
                {
                    Logger?.Invoke($"{_header}already connected");
                    return;
                }

                int retryCount = 0;
                long started = MonotonicTime.GetTimestamp();
                Exception lastException = null;

                while (true)
                {
                    int remainingMs = MonotonicTime.RemainingMilliseconds(started, _settings.ConnectTimeoutMs);
                    if (remainingMs <= 0)
                    {
                        SafeCloseClient();
                        throw lastException == null
                            ? new TimeoutException($"Timeout connecting to {ServerIpPort}")
                            : new TimeoutException($"Timeout connecting to {ServerIpPort}", lastException);
                    }

                    try
                    {
                        string msg = $"{_header}attempting connection to {_serverIp}:{_serverPort}";
                        if (retryCount > 0) msg += $" ({retryCount} retries)";
                        Logger?.Invoke(msg);

                        BeginConnect();
                        ConnectCore(Math.Min(remainingMs, 1000));
                        EndConnect();
                        Logger?.Invoke($"{_header}connected to {_serverIp}:{_serverPort}");
                        return;
                    }
                    catch (Exception e) when (
                        e is TimeoutException
                        || e is SocketException
                        || e is ObjectDisposedException
                        || e is IOException
                        || e is AuthenticationException)
                    {
                        lastException = e;
                        Logger?.Invoke($"{_header}failed connecting to {_serverIp}:{_serverPort}: {e.Message}");
                        SafeCloseClient();
                    }
                    finally
                    {
                        retryCount++;
                    }
                }
            }
            finally
            {
                _connectMutex.Release();
            }
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                Logger?.Invoke($"{_header}already disconnected");
                return;
            }

            Logger?.Invoke($"{_header}disconnecting from {ServerIpPort}");

            _tokenSource.Cancel();
            CloseTransport();
            WaitCompletion();
            _isConnected = false;
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (!IsConnected)
            {
                Logger?.Invoke($"{_header}already disconnected");
                return;
            }

            Logger?.Invoke($"{_header}disconnecting from {ServerIpPort}");

            _tokenSource.Cancel();
            CloseTransport();
            await WaitCompletionAsync();
            _isConnected = false;
        }

        /// <summary>
        /// Send data to the server.
        /// </summary>
        /// <param name="data">String containing data to send.</param>
        public void Send(string data)
        {
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (!_isConnected) throw new IOException("Not connected to the server; use Connect() first.");

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            this.Send(bytes);
        }

        /// <summary>
        /// Send data to the server.
        /// </summary> 
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (!_isConnected) throw new IOException("Not connected to the server; use Connect() first.");
            SendInternal(data);
        }

        /// <summary>
        /// Send data to the server.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the source stream to send.</param>
        /// <param name="stream">Stream containing the data to send.</param>
        public void Send(long contentLength, Stream stream)
        {
            if (contentLength < 1) return;
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
            if (!_isConnected) throw new IOException("Not connected to the server; use Connect() first.");

            SendInternal(contentLength, stream);
        }

        /// <summary>
        /// Send data to the server asynchronously.
        /// </summary>
        /// <param name="data">String containing data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string data, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (!_isConnected) throw new IOException("Not connected to the server; use Connect() first.");
            if (token == default(CancellationToken)) token = _token;

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            await SendInternalAsync(bytes, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the server asynchronously.
        /// </summary> 
        /// <param name="data">Byte array containing data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(byte[] data, CancellationToken token = default)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (!_isConnected) throw new IOException("Not connected to the server; use Connect() first.");
            if (token == default(CancellationToken)) token = _token;
            await SendInternalAsync(data, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the server asynchronously.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the source stream to send.</param>
        /// <param name="stream">Stream containing the data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (contentLength < 1) return;
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
            if (!_isConnected) throw new IOException("Not connected to the server; use Connect() first.");
            if (token == default(CancellationToken)) token = _token;

            await SendInternalAsync(contentLength, stream, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of the TCP client.
        /// </summary>
        /// <param name="disposing">Dispose of resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isConnected = false;

                if (_tokenSource != null)
                {
                    if (!_tokenSource.IsCancellationRequested)
                    {
                        _tokenSource.Cancel();
                        _tokenSource.Dispose();
                    }
                }

                if (_sslStream != null)
                {
                    _sslStream.Close();
                    _sslStream.Dispose();
                }

                if (_networkStream != null)
                {
                    _networkStream.Close();
                    _networkStream.Dispose();
                }

                if (_client != null)
                {
                    _client.Close();
                    _client.Dispose();
                }

                if (_asyncDataReceivedDispatcher != null)
                {
                    _asyncDataReceivedDispatcher.Dispose();
                    _asyncDataReceivedDispatcher = null;
                }

                Logger?.Invoke($"{_header}dispose complete");
            }
        }

        private void InitializeClient(bool ssl, string pfxCertFilename, string pfxPassword, X509Certificate2 sslCert)
        {
            _ssl = ssl;
            _pfxCertFilename = pfxCertFilename;
            _pfxPassword = pfxPassword;

            _client = _settings.LocalEndpoint == null ? new TcpClient(_addressFamily) : new TcpClient(_settings.LocalEndpoint);
            _client.NoDelay = _settings.NoDelay;

            _sslStream = null;
            _sslCert = null;
            _sslCertCollection = null;

            if (_ssl)
            {
                if (sslCert != null)
                {
                    _sslCert = sslCert;
                    _sslCertCollection = new X509Certificate2Collection { _sslCert };
                }
                else if (string.IsNullOrEmpty(pfxPassword))
                {
#if NET9_0_OR_GREATER
                    _sslCert = X509CertificateLoader.LoadPkcs12FromFile(pfxCertFilename, null);
#else
                    _sslCert = new X509Certificate2(pfxCertFilename);
#endif
                }
                else
                {
#if NET9_0_OR_GREATER
                    _sslCert = X509CertificateLoader.LoadPkcs12FromFile(pfxCertFilename, pfxPassword);
#else
                    _sslCert = new X509Certificate2(pfxCertFilename, pfxPassword);
#endif
                }

                _sslCertCollection = new X509Certificate2Collection
                {
                    _sslCert
                };
            }
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return _settings.AcceptInvalidCertificates;
        }

        private async Task DataReceiver(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _client != null && _client.Connected)
            {
                try
                {
                    bool ready = await WaitForReadReadyAsync(token).ConfigureAwait(false);
                    if (!ready)
                    {
                        continue;
                    }

                    var data = await DataReadAsync(token).ConfigureAwait(false);
                    if (data.Array == null)
                    {
                        continue;
                    }

                    _lastActivityTimestamp = MonotonicTime.GetTimestamp();
                    QueueDataReceived(data);
                    _statistics.AddReceivedBytes(data.Count);
                }
                catch (AggregateException)
                {
                    Logger?.Invoke($"{_header}data receiver canceled, disconnected");
                    break;
                }
                catch (IOException)
                {
                    Logger?.Invoke($"{_header}data receiver canceled, disconnected");
                    break;
                }
                catch (SocketException)
                {
                    Logger?.Invoke($"{_header}data receiver canceled, disconnected");
                    break;
                }
                catch (TaskCanceledException)
                {
                    Logger?.Invoke($"{_header}data receiver task canceled, disconnected");
                    break;
                }
                catch (OperationCanceledException)
                {
                    Logger?.Invoke($"{_header}data receiver operation canceled, disconnected");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Logger?.Invoke($"{_header}data receiver canceled due to disposal, disconnected");
                    break;
                }
                catch (Exception e)
                {
                    Logger?.Invoke($"{_header}data receiver exception:{Environment.NewLine}{e}{Environment.NewLine}");
                    break;
                }
            }

            Logger?.Invoke($"{_header}disconnection detected");

            _isConnected = false;

            if (!_isTimeout) _events.HandleClientDisconnected(this, new ConnectionEventArgs(ServerIpPort, DisconnectReason.Normal));
            else _events.HandleClientDisconnected(this, new ConnectionEventArgs(ServerIpPort, DisconnectReason.Timeout));

            Dispose();
        }

        private async Task<ArraySegment<byte>> DataReadAsync(CancellationToken token)
        {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_settings.StreamBufferSize);
#else
            byte[] buffer = new byte[_settings.StreamBufferSize];
#endif
            int read = 0;

            try
            {
                read = !_ssl
                    ? await _networkStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)
                    : await _sslStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                if (read > 0)
                {
                    byte[] payload = new byte[read];
                    Buffer.BlockCopy(buffer, 0, payload, 0, read);
                    return new ArraySegment<byte>(payload, 0, read);
                }

                IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                TcpConnectionInformation[] tcpConnections = ipProperties.GetActiveTcpConnections()
                    .Where(x => x.LocalEndPoint.Equals(this._client.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(this._client.Client.RemoteEndPoint)).ToArray();

                var isOk = false;

                if (tcpConnections != null && tcpConnections.Length > 0)
                {
                    TcpState stateOfConnection = tcpConnections.First().State;
                    if (stateOfConnection == TcpState.Established)
                    {
                        isOk = true;
                    }
                }

                if (!isOk)
                {
                    _tokenSource.Cancel();
                }

                throw new SocketException();
            }
            finally
            {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                ArrayPool<byte>.Shared.Return(buffer);
#endif
            }
        }

        private void SendInternal(byte[] data)
        {
            try
            {
                _sendLock.Wait();

                if (!_ssl) _networkStream.Write(data, 0, data.Length);
                else _sslStream.Write(data, 0, data.Length);

                if (!_ssl) _networkStream.Flush();
                else _sslStream.Flush();

                _statistics.AddSentBytes(data.Length);
                _events.HandleDataSent(this, new DataSentEventArgs(ServerIpPort, data.Length));
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task SendInternalAsync(byte[] data, CancellationToken token)
        {
            bool sendLockHeld = false;
            try
            {
                await _sendLock.WaitAsync(token).ConfigureAwait(false);
                sendLockHeld = true;

                if (!_ssl) await _networkStream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                else await _sslStream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);

                if (!_ssl) await _networkStream.FlushAsync(token).ConfigureAwait(false);
                else await _sslStream.FlushAsync(token).ConfigureAwait(false);

                _statistics.AddSentBytes(data.Length);
                _events.HandleDataSent(this, new DataSentEventArgs(ServerIpPort, data.Length));
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (sendLockHeld)
                {
                    _sendLock.Release();
                }
            }
        }

        private void SendInternal(long contentLength, Stream stream)
        {
            long bytesRemaining = contentLength;
            int bytesRead = 0;
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_settings.StreamBufferSize);
#else
            byte[] buffer = new byte[_settings.StreamBufferSize];
#endif

            try
            {
                _sendLock.Wait();

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    if (!_ssl) _networkStream.Write(buffer, 0, bytesRead);
                    else _sslStream.Write(buffer, 0, bytesRead);

                    bytesRemaining -= bytesRead;
                    _statistics.AddSentBytes(bytesRead);
                }

                if (!_ssl) _networkStream.Flush();
                else _sslStream.Flush();
                _events.HandleDataSent(this, new DataSentEventArgs(ServerIpPort, contentLength));
            }
            finally
            {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                ArrayPool<byte>.Shared.Return(buffer);
#endif
                _sendLock.Release();
            }
        }

        private async Task SendInternalAsync(long contentLength, Stream stream, CancellationToken token)
        {
            bool sendLockHeld = false;
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_settings.StreamBufferSize);
#else
            byte[] buffer = new byte[_settings.StreamBufferSize];
#endif
            try
            {
                long bytesRemaining = contentLength;
                int bytesRead = 0;

                await _sendLock.WaitAsync(token).ConfigureAwait(false);
                sendLockHeld = true;

                while (bytesRemaining > 0)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    if (!_ssl) await _networkStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                    else await _sslStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);

                    bytesRemaining -= bytesRead;
                    _statistics.AddSentBytes(bytesRead);
                }

                if (!_ssl) await _networkStream.FlushAsync(token).ConfigureAwait(false);
                else await _sslStream.FlushAsync(token).ConfigureAwait(false);
                _events.HandleDataSent(this, new DataSentEventArgs(ServerIpPort, contentLength));
            }
            catch (TaskCanceledException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                ArrayPool<byte>.Shared.Return(buffer);
#endif
                if (sendLockHeld)
                {
                    _sendLock.Release();
                }
            }
        }

        private void WaitCompletion()
        {
            WaitForTaskCompletion(_dataReceiver, 2000);
            WaitForTaskCompletion(_idleServerMonitor, 2000);
            WaitForTaskCompletion(_connectionMonitor, 2000);
        }

        private async Task WaitCompletionAsync()
        {
            await WaitForTaskCompletionAsync(_dataReceiver, 2000).ConfigureAwait(false);
            await WaitForTaskCompletionAsync(_idleServerMonitor, 2000).ConfigureAwait(false);
            await WaitForTaskCompletionAsync(_connectionMonitor, 2000).ConfigureAwait(false);
        }

        private void EnableKeepalives()
        {
            // issues with definitions: https://github.com/dotnet/sdk/issues/14540

            try
            {
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER

                // NETCOREAPP3_1_OR_GREATER catches .NET 5.0

                _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _keepalive.TcpKeepAliveTime);
                _client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _keepalive.TcpKeepAliveInterval);

                // Windows 10 version 1703 or later

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && Environment.OSVersion.Version >= new Version(10, 0, 15063))
                {
                    _client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _keepalive.TcpKeepAliveRetryCount);
                }

#elif NETFRAMEWORK

                byte[] keepAlive = new byte[12];

                // Turn keepalive on
                Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);

                // Set TCP keepalive time
                Buffer.BlockCopy(BitConverter.GetBytes((uint)_keepalive.TcpKeepAliveTimeMilliseconds), 0, keepAlive, 4, 4);

                // Set TCP keepalive interval
                Buffer.BlockCopy(BitConverter.GetBytes((uint)_keepalive.TcpKeepAliveIntervalMilliseconds), 0, keepAlive, 8, 4);

                // Set keepalive settings on the underlying Socket
                _client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception)
            {
                Logger?.Invoke($"{_header}keepalives not supported on this platform, disabled");
                _keepalive.EnableTcpKeepAlives = false;
            }
        }

        private async Task IdleServerMonitor()
        {
            while (!_token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_settings.IdleServerEvaluationIntervalMs, _token).ConfigureAwait(false);

                    if (_settings.IdleServerTimeoutMs == 0) continue;

                    if (MonotonicTime.HasElapsed(_lastActivityTimestamp, _settings.IdleServerTimeoutMs))
                    {
                        Logger?.Invoke($"{_header}disconnecting from {ServerIpPort} due to timeout");
                        _isConnected = false;
                        _isTimeout = true;
                        _tokenSource.Cancel(); // DataReceiver will fire events including dispose
                        CloseTransport();
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ConnectedMonitor()
        {
            while (!_token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_settings.ConnectionLostEvaluationIntervalMs, _token).ConfigureAwait(false);

                    if (!_isConnected)
                        continue; //Just monitor connected clients

                    if (!PollSocket())
                    {
                        Logger?.Invoke($"{_header}disconnecting from {ServerIpPort} due to connection lost");
                        _isConnected = false;
                        _tokenSource.Cancel(); // DataReceiver will fire events including dispose
                        CloseTransport();
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private bool PollSocket()
        {
            try
            {
                if (_client.Client == null || !_client.Client.Connected)
                    return false;

                /* pear to the documentation on Poll:
                 * When passing SelectMode.SelectRead as a parameter to the Poll method it will return 
                 * -either- true if Socket.Listen(Int32) has been called and a connection is pending;
                 * -or- true if data is available for reading; 
                 * -or- true if the connection has been closed, reset, or terminated; 
                 * otherwise, returns false
                 */
                if (!_client.Client.Poll(0, SelectMode.SelectRead))
                    return true;

                var clientSentData = _client.Client.Receive(_pollBuffer, SocketFlags.Peek) != 0;
                return clientSentData; //False here though Poll() succeeded means we had a disconnect!
            }
            catch (SocketException ex)
            {
                Logger?.Invoke($"{_header}poll socket from {ServerIpPort} failed with ex = {ex}");
                return ex.SocketErrorCode == SocketError.TimedOut;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void InitializeConnectedStreams()
        {
            _networkStream = _client.GetStream();

            if (!_ssl)
            {
                return;
            }

            if (_settings.AcceptInvalidCertificates)
            {
                _sslStream = new SslStream(_networkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
            }
            else if (_settings.CertificateValidationCallback != null)
            {
                _sslStream = new SslStream(_networkStream, false, new RemoteCertificateValidationCallback(_settings.CertificateValidationCallback));
            }
            else
            {
                _sslStream = new SslStream(_networkStream, false);
            }
        }

        private void ValidateAuthenticatedStream()
        {
            if (!_sslStream.IsEncrypted) throw new AuthenticationException("Stream is not encrypted");
            if (!_sslStream.IsAuthenticated) throw new AuthenticationException("Stream is not authenticated");
            if (_settings.MutuallyAuthenticate && !_sslStream.IsMutuallyAuthenticated) throw new AuthenticationException("Mutual authentication failed");
        }

        private void CompleteConnect()
        {
            if (_keepalive.EnableTcpKeepAlives) EnableKeepalives();

            _isConnected = true;
            _lastActivityTimestamp = MonotonicTime.GetTimestamp();
            _isTimeout = false;
            _events.HandleConnected(this, new ConnectionEventArgs(ServerIpPort));
            _dataReceiver = DataReceiver(_token);
            _idleServerMonitor = IdleServerMonitor();
            _connectionMonitor = ConnectedMonitor();
        }

        private void EnsureAsyncDataReceivedDispatcher()
        {
            if (_asyncDataReceivedDispatcher != null)
            {
                return;
            }

            int workerCount = Math.Min(Math.Max(Environment.ProcessorCount, 2), 4);
            _asyncDataReceivedDispatcher = new AsyncEventDispatcher<DataReceivedEventArgs>(
                args => _events.HandleDataReceived(this, args),
                workerCount);
        }

        private void QueueDataReceived(ArraySegment<byte> data)
        {
            var args = new DataReceivedEventArgs(ServerIpPort, data);
            if (_settings.UseAsyncDataReceivedEvents)
            {
                EnsureAsyncDataReceivedDispatcher();
                _asyncDataReceivedDispatcher.Enqueue(args);
                return;
            }

            _events.HandleDataReceived(this, args);
        }

        private async Task<bool> WaitForReadReadyAsync(CancellationToken token)
        {
            if (_client?.Client == null)
            {
                return false;
            }

            await Task.Yield();

            long started = MonotonicTime.GetTimestamp();
            int sliceMs = 1;

            while (!token.IsCancellationRequested)
            {
                Socket socket = _client?.Client;
                if (socket == null)
                {
                    return false;
                }

                if (socket.Poll(sliceMs * 1000, SelectMode.SelectRead))
                {
                    return true;
                }

                if (MonotonicTime.HasElapsed(started, _settings.ReadTimeoutMs))
                {
                    return false;
                }
            }

            token.ThrowIfCancellationRequested();
            return false;
        }

        private void CloseTransport()
        {
            try
            {
                _sslStream?.Close();
            }
            catch
            {
            }

            try
            {
                _networkStream?.Close();
            }
            catch
            {
            }

            try
            {
                if (_client?.Client != null && _client.Client.Connected)
                {
                    _client.Client.Shutdown(SocketShutdown.Both);
                }
            }
            catch
            {
            }

            try
            {
                _client?.Close();
            }
            catch
            {
            }
        }

        private void SafeCloseClient()
        {
            try
            {
                _client?.Close();
            }
            catch
            {
            }
        }

        private void WaitForTaskCompletion(Task task, int timeoutMs)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                task.Wait(timeoutMs);
            }
            catch (AggregateException ex) when (
                ex.InnerException is TaskCanceledException
                || ex.InnerException is OperationCanceledException
                || ex.InnerException is ObjectDisposedException)
            {
                Logger?.Invoke("Awaiting a canceled task");
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task WaitForTaskCompletionAsync(Task task, int timeoutMs)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                Task completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (completedTask == task)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                Logger?.Invoke("Awaiting a canceled task");
            }
            catch (OperationCanceledException)
            {
                Logger?.Invoke("Awaiting a canceled task");
            }
            catch (ObjectDisposedException)
            {
            }
        }

        #endregion
    }
}
