namespace SuperSimpleTcp
{
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    using System.Buffers;
#endif
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
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
    /// SimpleTcp server with SSL support.  
    /// Set the ClientConnected, ClientDisconnected, and DataReceived events.  
    /// Once set, use Start() to begin listening for connections.
    /// </summary>
    public class SimpleTcpServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates if the server is listening for connections.
        /// </summary>
        public bool IsListening
        {
            get
            {
                return _isListening;
            }
        }

        /// <summary>
        /// SimpleTcp server settings.
        /// </summary>
        public SimpleTcpServerSettings Settings
        {
            get
            {
                return _settings;
            }
            set
            {
                if (value == null) _settings = new SimpleTcpServerSettings();
                else _settings = value;
            }
        }

        /// <summary>
        /// SimpleTcp server events.
        /// </summary>
        public SimpleTcpServerEvents Events
        {
            get
            {
                return _events;
            }
            set
            {
                if (value == null) _events = new SimpleTcpServerEvents();
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
        /// Retrieve the number of current connected clients.
        /// </summary>
        public int Connections
        {
            get
            {
                return _clients.Count;
            }
        }

        /// <summary>
        /// The IP address on which the server is configured to listen.
        /// </summary>
        public IPAddress IpAddress
        {
            get
            {
                return _ipAddress;
            }
        }

        /// <summary>
        /// The IPEndPoint on which the server is configured to listen.
        /// </summary>
        public EndPoint Endpoint
        {
            get
            {
                return _listener == null ? null : ((IPEndPoint)_listener.LocalEndpoint);
            }
        }
        /// <summary>
        /// The port on which the server is configured to listen.
        /// </summary>
        public int Port
        {
            get
            {
                return _listener == null ? 0 : ((IPEndPoint)_listener.LocalEndpoint).Port;
            }
        }

        /// <summary>
        /// Method to invoke to send a log message.
        /// </summary>
        public Action<string> Logger = null;

        #endregion

        #region Private-Members

        private readonly string _header = "[SimpleTcp.Server] ";
        private SimpleTcpServerSettings _settings = new SimpleTcpServerSettings();
        private SimpleTcpServerEvents _events = new SimpleTcpServerEvents();
        private SimpleTcpKeepaliveSettings _keepalive = new SimpleTcpKeepaliveSettings();
        private SimpleTcpStatistics _statistics = new SimpleTcpStatistics();

        private readonly string _listenerIp = null;
        private readonly IPAddress _ipAddress = null;
        private readonly int _port = 0;
        private readonly bool _ssl = false;

        private readonly X509Certificate2 _sslCertificate = null;
        private readonly X509Certificate2Collection _sslCertificateCollection = null;

        private readonly ConcurrentDictionary<string, ClientMetadata> _clients = new ConcurrentDictionary<string, ClientMetadata>();

        private TcpListener _listener = null;
        private bool _isListening = false;

        private CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private CancellationToken _token;
        private CancellationTokenSource _listenerTokenSource = new CancellationTokenSource();
        private CancellationToken _listenerToken;
        private Task _acceptConnections = null;
        private Task _idleClientMonitor = null;
        private AsyncEventDispatcher<DataReceivedWorkItem> _asyncDataReceivedDispatcher = null;
        private InstanceTelemetry _telemetry = null;
        private HashSet<string> _permittedIpLookup = null;
        private List<string> _permittedIpSource = null;
        private int _permittedIpLookupCount = -1;
        private HashSet<string> _blockedIpLookup = null;
        private List<string> _blockedIpSource = null;
        private int _blockedIpLookupCount = -1;

        private InstanceTelemetry Telemetry
        {
            get
            {
                if (_telemetry == null) _telemetry = new InstanceTelemetry(SimpleTcpTelemetryNames.RoleServer, _ssl, () => _settings.Telemetry);
                return _telemetry;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP server without SSL.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        public SimpleTcpServer(string ipPort)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _listenerIp, out _port);

            if (_port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (string.IsNullOrEmpty(_listenerIp))
            {
                _ipAddress = IPAddress.Loopback;
                _listenerIp = _ipAddress.ToString();
            }
            else if (_listenerIp == "*" || _listenerIp == "+")
            {
                _ipAddress = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(_listenerIp, out _ipAddress))
                {
                    _ipAddress = Dns.GetHostEntry(_listenerIp).AddressList[0];
                    _listenerIp = _ipAddress.ToString();
                } 
            }

            _isListening = false;
            _token = _tokenSource.Token;
        }

        /// <summary>
        /// Instantiates the TCP server without SSL.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="listenerIp">The listener IP address or hostname.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        public SimpleTcpServer(string listenerIp, int port)
        {
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _listenerIp = listenerIp;
            _port = port;

            if (string.IsNullOrEmpty(_listenerIp))
            {
                _ipAddress = IPAddress.Loopback;
                _listenerIp = _ipAddress.ToString();
            }
            else if (_listenerIp == "*" || _listenerIp == "+")
            {
                _ipAddress = IPAddress.Any;
                _listenerIp = listenerIp;
            }
            else
            { 
                if (!IPAddress.TryParse(_listenerIp, out _ipAddress))
                {
                    _ipAddress = Dns.GetHostEntry(listenerIp).AddressList[0];
                    _listenerIp = _ipAddress.ToString();
                } 
            }
             
            _isListening = false;
            _token = _tokenSource.Token; 
        }

        /// <summary>
        /// Instantiates the TCP server.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpServer(string ipPort, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _listenerIp, out _port);
            if (_port < 0) throw new ArgumentException("Port must be zero or greater.");

            if (string.IsNullOrEmpty(_listenerIp))
            {
                _ipAddress = IPAddress.Loopback;
                _listenerIp = _ipAddress.ToString();
            }
            else if (_listenerIp == "*" || _listenerIp == "+")
            {
                _ipAddress = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(_listenerIp, out _ipAddress))
                {
                    _ipAddress = Dns.GetHostEntry(_listenerIp).AddressList[0];
                    _listenerIp = _ipAddress.ToString();
                }
            }

            _ssl = ssl;
            _isListening = false;
            _token = _tokenSource.Token;

            if (_ssl)
            {
                if (string.IsNullOrEmpty(pfxPassword))
                {
#if NET9_0_OR_GREATER
                    _sslCertificate = X509CertificateLoader.LoadPkcs12FromFile(pfxCertFilename, null);
#else
                    _sslCertificate = new X509Certificate2(pfxCertFilename);
#endif
                }
                else
                {
#if NET9_0_OR_GREATER
                    _sslCertificate = X509CertificateLoader.LoadPkcs12FromFile(pfxCertFilename, pfxPassword);
#else
                    _sslCertificate = new X509Certificate2(pfxCertFilename, pfxPassword);
#endif
                }

                _sslCertificateCollection = new X509Certificate2Collection
                {
                    _sslCertificate
                };
            } 
        }

        /// <summary>
        /// Instantiates the TCP server.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="listenerIp">The listener IP address or hostname.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpServer(string listenerIp, int port, bool ssl, string pfxCertFilename, string pfxPassword)
        { 
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _listenerIp = listenerIp;
            _port = port;

            if (string.IsNullOrEmpty(_listenerIp))
            {
                _ipAddress = IPAddress.Loopback;
                _listenerIp = _ipAddress.ToString();
            }
            else if (_listenerIp == "*" || _listenerIp == "+")
            {
                _ipAddress = IPAddress.Any; 
            }
            else
            {
                if (!IPAddress.TryParse(_listenerIp, out _ipAddress))
                {
                    _ipAddress = Dns.GetHostEntry(listenerIp).AddressList[0];
                    _listenerIp = _ipAddress.ToString();
                }
            }
             
            _ssl = ssl;
            _isListening = false;
            _token = _tokenSource.Token;

            if (_ssl)
            {
                if (string.IsNullOrEmpty(pfxPassword))
                {
#if NET9_0_OR_GREATER
                    _sslCertificate = X509CertificateLoader.LoadPkcs12FromFile(pfxCertFilename, null);
#else
                    _sslCertificate = new X509Certificate2(pfxCertFilename);
#endif
                }
                else
                {
#if NET9_0_OR_GREATER
                    _sslCertificate = X509CertificateLoader.LoadPkcs12FromFile(pfxCertFilename, pfxPassword);
#else
                    _sslCertificate = new X509Certificate2(pfxCertFilename, pfxPassword);
#endif
                }

                _sslCertificateCollection = new X509Certificate2Collection
                {
                    _sslCertificate
                };
            } 
        }

        /// <summary>
        /// Instantiates the TCP server with SSL.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="listenerIp">The listener IP address or hostname.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        /// <param name="certificate">Byte array containing the certificate.</param>
        public SimpleTcpServer(string listenerIp, int port, byte[] certificate)
        {
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            _listenerIp = listenerIp;
            _port = port;

            if (string.IsNullOrEmpty(_listenerIp))
            {
                _ipAddress = IPAddress.Loopback;
                _listenerIp = _ipAddress.ToString();
            }
            else if (_listenerIp == "*" || _listenerIp == "+")
            {
                _ipAddress = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(_listenerIp, out _ipAddress))
                {
                    _ipAddress = Dns.GetHostEntry(listenerIp).AddressList[0];
                    _listenerIp = _ipAddress.ToString();
                }
            }

            _ssl = true;
#if NET9_0_OR_GREATER
            _sslCertificate = X509CertificateLoader.LoadPkcs12(certificate, null);
#else
            _sslCertificate = new X509Certificate2(certificate);
#endif
            _sslCertificateCollection = new X509Certificate2Collection
            {
                _sslCertificate
            };

            _isListening = false;
            _token = _tokenSource.Token;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of the TCP server.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start accepting connections.
        /// </summary>
        public void Start()
        {
            if (_isListening) throw new InvalidOperationException("SimpleTcpServer is already running.");

            _listener = new TcpListener(_ipAddress, _port);
            _listener.Server.NoDelay = _settings.NoDelay;
            _listener.Start();
            _isListening = true;

            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;
            _listenerTokenSource = new CancellationTokenSource();
            _listenerToken = _listenerTokenSource.Token;

            _statistics = new SimpleTcpStatistics();
            EnsureAsyncDataReceivedDispatcher();
             
            if (_idleClientMonitor == null)
            {
                _idleClientMonitor = IdleClientMonitor();
            }

            _acceptConnections = AcceptConnections();
        }

        /// <summary>
        /// Start accepting connections.
        /// </summary>
        /// <returns>Task.</returns>
        public Task StartAsync()
        {
            if (_isListening) throw new InvalidOperationException("SimpleTcpServer is already running.");

            _listener = new TcpListener(_ipAddress, _port);
            _listener.Server.NoDelay = _settings.NoDelay;

            if (_keepalive.EnableTcpKeepAlives) EnableKeepalives();

            _listener.Start();
            _isListening = true;

            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;
            _listenerTokenSource = new CancellationTokenSource();
            _listenerToken = _listenerTokenSource.Token;

            _statistics = new SimpleTcpStatistics();
            EnsureAsyncDataReceivedDispatcher();

            if (_idleClientMonitor == null)
            {
                _idleClientMonitor = IdleClientMonitor();
            }

            _acceptConnections = AcceptConnections();
            return _acceptConnections;
        }

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public void Stop()
        {
            _isListening = false;

            if (_listenerTokenSource != null && !_listenerTokenSource.IsCancellationRequested)
            {
                _listenerTokenSource.Cancel();
            }

            if (_listener != null)
            {
                _listener.Stop();
            }

            WaitForTaskCompletion(_acceptConnections, 2000);
            _acceptConnections = null;

            Logger?.Invoke($"{_header}stopped");
        }

        /// <summary>
        /// Retrieve a list of client IP:port connected to the server.
        /// </summary>
        /// <returns>IEnumerable of strings, each containing client IP:port.</returns>
        public IEnumerable<string> GetClients()
        {
            List<string> clients = new List<string>(_clients.Keys);
            return clients;
        }

        /// <summary>
        /// Determines if a client is connected by its IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <returns>True if connected.</returns>
        public bool IsConnected(string ipPort)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            return (_clients.TryGetValue(ipPort, out _));
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">String containing data to send.</param>
        public void Send(string ipPort, string data)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            SendInternal(ipPort, bytes);
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(string ipPort, byte[] data)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            SendInternal(ipPort, data);
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="contentLength">The number of bytes to read from the source stream to send.</param>
        /// <param name="stream">Stream containing the data to send.</param>
        public void Send(string ipPort, long contentLength, Stream stream)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) return;
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");

            SendInternal(ipPort, contentLength, stream);
        }

        /// <summary>
        /// Send data to the specified client by IP:port asynchronously.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">String containing data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string ipPort, string data, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (token == default(CancellationToken)) token = _token;

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            await SendInternalAsync(ipPort, bytes, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the specified client by IP:port asynchronously.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">Byte array containing data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string ipPort, byte[] data, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (token == default(CancellationToken)) token = _token;
            await SendInternalAsync(ipPort, data, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the specified client by IP:port asynchronously.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="contentLength">The number of bytes to read from the source stream to send.</param>
        /// <param name="stream">Stream containing the data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string ipPort, long contentLength, Stream stream, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) return;
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
            if (token == default(CancellationToken)) token = _token;

            await SendInternalAsync(ipPort, contentLength, stream, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Disconnects the specified client.
        /// </summary>
        /// <param name="ipPort">IP:port of the client.</param>
        public void DisconnectClient(string ipPort)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            if (!_clients.TryGetValue(ipPort, out ClientMetadata client))
            {
                Logger?.Invoke($"{_header}unable to find client: {ipPort}");
            }
            else
            {
                client.TryMarkDisconnectReason(DisconnectReason.Kicked);
                Logger?.Invoke($"{_header}kicking: {ipPort}");
            }

            DisconnectClientInternal(client);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of the TCP server.
        /// </summary>
        /// <param name="disposing">Dispose of resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_clients != null && _clients.Count > 0)
                    {
                        foreach (KeyValuePair<string, ClientMetadata> curr in _clients)
                        {
                            curr.Value.Dispose();
                            Logger?.Invoke($"{_header}disconnected client: {curr.Key}");
                        } 
                    }

                    if (_tokenSource != null)
                    {
                        if (!_tokenSource.IsCancellationRequested)
                        {
                            _tokenSource.Cancel();
                        }

                        _tokenSource.Dispose();
                    }

                    if (_listener != null && _listener.Server != null)
                    {
                        _listener.Server.Close();
                        _listener.Server.Dispose();
                    }

                    if (_listener != null)
                    {
                        _listener.Stop();
                    }

                    if (_asyncDataReceivedDispatcher != null)
                    {
                        _asyncDataReceivedDispatcher.Dispose();
                        Telemetry.DispatchDropped(_asyncDataReceivedDispatcher.DrainPending());
                        _asyncDataReceivedDispatcher = null;
                    }
                }
                catch (Exception e)
                {
                    Logger?.Invoke($"{_header}dispose exception:{Environment.NewLine}{e}{Environment.NewLine}");
                }

                _isListening = false;

                Logger?.Invoke($"{_header}disposed");
            }
        }
         
        private bool IsClientConnected(ClientMetadata client)
        {
            if (client == null) return false;
            if (client.Client == null) return false;

            var state = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                    .FirstOrDefault(x =>
                        x.LocalEndPoint.Equals(client.Client.Client.LocalEndPoint)
                        && x.RemoteEndPoint.Equals(client.Client.Client.RemoteEndPoint));

            if (state == default(TcpConnectionInformation)
                || state.State == TcpState.Unknown
                || state.State == TcpState.FinWait1
                || state.State == TcpState.FinWait2
                || state.State == TcpState.Closed
                || state.State == TcpState.Closing
                || state.State == TcpState.CloseWait)
            {
                return false;
            }

            if ((client.Client.Client.Poll(0, SelectMode.SelectWrite)) && (!client.Client.Client.Poll(0, SelectMode.SelectError)))
            {
                try
                {
                    return client.Client.Client.Receive(client.ProbeBuffer, SocketFlags.Peek) != 0;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private async Task AcceptConnections()
        {
            InstanceTelemetry.DetachAmbientSpan();

            int connectionLimit = _settings.MaxConnections;
            Telemetry.ListenerStarted(connectionLimit);

            try
            {
                while (!_listenerToken.IsCancellationRequested)
                {
                    ClientMetadata client = null;
                    Activity acceptActivity = null;
                    long acceptStart = 0;

                    try
                    {
                        TcpClient tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        acceptStart = InstanceTelemetry.Timestamp();
                        tcpClient.NoDelay = _settings.NoDelay;
                        string clientIpPort = tcpClient.Client.RemoteEndPoint.ToString();

                        acceptActivity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanAccept, ActivityKind.Server);
                        InstanceTelemetry.SetPeer(acceptActivity, clientIpPort);
                        InstanceTelemetry.SetTag(acceptActivity, SimpleTcpTelemetryNames.AttributeServerAddress, _listenerIp);
                        InstanceTelemetry.SetTag(acceptActivity, SimpleTcpTelemetryNames.AttributeServerPort, Port);

                        string clientIp = null;
                        int clientPort = 0;
                        Common.ParseIpPort(clientIpPort, out clientIp, out clientPort);

                        if (!IsClientPermitted(clientIp))
                        {
                            Logger?.Invoke($"{_header}rejecting connection from {clientIp} (not permitted)");
                            tcpClient.Close();
                            RecordRejected(acceptActivity, acceptStart, SimpleTcpTelemetryNames.RejectNotPermitted, false);
                            continue;
                        }

                        if (IsClientBlocked(clientIp))
                        {
                            Logger?.Invoke($"{_header}rejecting connection from {clientIp} (blocked)");
                            tcpClient.Close();
                            RecordRejected(acceptActivity, acceptStart, SimpleTcpTelemetryNames.RejectBlocked, false);
                            continue;
                        }

                        if (_clients.Count >= _settings.MaxConnections)
                        {
                            Logger?.Invoke($"{_header}rejecting connection from {clientIpPort} (maximum connections {_settings.MaxConnections} reached)");
                            tcpClient.Close();
                            RecordRejected(acceptActivity, acceptStart, SimpleTcpTelemetryNames.RejectMaxConnections, true);
                            continue;
                        }

                        client = new ClientMetadata(tcpClient);

                        if (_ssl)
                        {
                            if (_settings.AcceptInvalidCertificates)
                            {
                                client.SslStream = new SslStream(client.NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                            }
                            else if(_settings.CertificateValidationCallback != null)
                            {
                                client.SslStream = new SslStream(client.NetworkStream, false, new RemoteCertificateValidationCallback(_settings.CertificateValidationCallback));
                            }
                            else
                            {
                                client.SslStream = new SslStream(client.NetworkStream, false);
                            }

                            using (CancellationTokenSource tlsCts = CancellationTokenSource.CreateLinkedTokenSource(_listenerToken, _token))
                            {
                                tlsCts.CancelAfter(3000);

                                bool success = await StartTls(client, tlsCts.Token).ConfigureAwait(false);
                                if (!success)
                                {
                                    client.Dispose();
                                    RecordRejected(acceptActivity, acceptStart, SimpleTcpTelemetryNames.RejectTlsFailed, true);
                                    continue;
                                }
                            }
                        }

                        if (!_clients.TryAdd(clientIpPort, client))
                        {
                            client.Dispose();
                            RecordRejected(acceptActivity, acceptStart, SimpleTcpTelemetryNames.RejectDuplicate, true);
                            continue;
                        }

                        client.ConnectedTimestamp = InstanceTelemetry.Timestamp();
                        client.UpdateLastSeen(MonotonicTime.GetTimestamp());
                        Telemetry.ConnectionOpened();
                        Logger?.Invoke($"{_header}starting data receiver for: {clientIpPort}");
                        _events.HandleClientConnected(this, new ConnectionEventArgs(clientIpPort));

                        if (_keepalive.EnableTcpKeepAlives) EnableKeepalives(tcpClient);

                        client.ReceiveTask = DataReceiver(client);

                        InstanceTelemetry.SetOutcome(acceptActivity, SimpleTcpTelemetryNames.OutcomeSuccess);
                        Telemetry.AcceptCompleted(SimpleTcpTelemetryNames.OutcomeSuccess, InstanceTelemetry.ElapsedSeconds(acceptStart));
                    }
                    catch (Exception ex)
                    {
                        if (ex is TaskCanceledException
                            || ex is OperationCanceledException
                            || ex is ObjectDisposedException
                            || ex is InvalidOperationException)
                        {
                            _isListening = false;
                            if (client != null) client.Dispose();
                            InstanceTelemetry.SetOutcome(acceptActivity, SimpleTcpTelemetryNames.OutcomeCanceled);
                            Logger?.Invoke($"{_header}stopped listening");
                            break;
                        }
                        else
                        {
                            if (client != null) client.Dispose();

                            // Stop() aborts the pending accept with a SocketException; that is shutdown, not a failure.
                            if (!_listenerToken.IsCancellationRequested)
                            {
                                InstanceTelemetry.SetException(acceptActivity, SimpleTcpTelemetryNames.OutcomeError, ex);
                                if (acceptStart != 0) Telemetry.AcceptCompleted(SimpleTcpTelemetryNames.OutcomeError, InstanceTelemetry.ElapsedSeconds(acceptStart));
                                Telemetry.Error(SimpleTcpTelemetryNames.OperationAccept, ex);
                            }

                            Logger?.Invoke($"{_header}exception while awaiting connections: {ex}");
                            continue;
                        }
                    }
                    finally
                    {
                        InstanceTelemetry.Stop(acceptActivity);
                    }
                }
            }
            finally
            {
                Telemetry.ListenerStopped(connectionLimit);
            }

            _isListening = false;
        }

        private void RecordRejected(Activity acceptActivity, long acceptStart, string rejectReason, bool isError)
        {
            InstanceTelemetry.SetTag(acceptActivity, SimpleTcpTelemetryNames.AttributeRejectReason, rejectReason);
            if (isError) InstanceTelemetry.SetFailure(acceptActivity, SimpleTcpTelemetryNames.OutcomeRejected, rejectReason);
            else InstanceTelemetry.SetOutcome(acceptActivity, SimpleTcpTelemetryNames.OutcomeRejected);

            Telemetry.ConnectionRejected(rejectReason);
            Telemetry.AcceptCompleted(SimpleTcpTelemetryNames.OutcomeRejected, InstanceTelemetry.ElapsedSeconds(acceptStart));
        }

        private async Task<bool> StartTls(ClientMetadata client, CancellationToken token)
        {
            Activity tlsActivity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanTlsHandshake, ActivityKind.Internal);
            long tlsStart = InstanceTelemetry.Timestamp();

            try
            {
                await client.SslStream.AuthenticateAsServerAsync(
                    _sslCertificate,
                    _settings.MutuallyAuthenticate,
                    SslProtocols.Tls12,
                    _settings.CheckCertificateRevocation).ConfigureAwait(false);

                if (!client.SslStream.IsEncrypted)
                {
                    Logger?.Invoke($"{_header}client {client.IpPort} not encrypted, disconnecting");
                    Telemetry.EndTlsHandshake(tlsActivity, tlsStart, SimpleTcpTelemetryNames.OutcomeError, new AuthenticationException("Stream is not encrypted"));
                    client.Dispose();
                    return false;
                }

                if (!client.SslStream.IsAuthenticated)
                {
                    Logger?.Invoke($"{_header}client {client.IpPort} not SSL/TLS authenticated, disconnecting");
                    Telemetry.EndTlsHandshake(tlsActivity, tlsStart, SimpleTcpTelemetryNames.OutcomeError, new AuthenticationException("Stream is not authenticated"));
                    client.Dispose();
                    return false;
                }

                if (_settings.MutuallyAuthenticate && !client.SslStream.IsMutuallyAuthenticated)
                {
                    Logger?.Invoke($"{_header}client {client.IpPort} failed mutual authentication, disconnecting");
                    Telemetry.EndTlsHandshake(tlsActivity, tlsStart, SimpleTcpTelemetryNames.OutcomeError, new AuthenticationException("Mutual authentication failed"));
                    client.Dispose();
                    return false;
                }

                InstanceTelemetry.SetTag(tlsActivity, SimpleTcpTelemetryNames.AttributeTlsProtocolVersion, InstanceTelemetry.TlsVersion(client.SslStream.SslProtocol));
                Telemetry.EndTlsHandshake(tlsActivity, tlsStart, SimpleTcpTelemetryNames.OutcomeSuccess, null);
            }
            catch (Exception e)
            {
                if (e is TaskCanceledException || e is OperationCanceledException)
                {
                    Logger?.Invoke($"{_header}client {client.IpPort} timeout during SSL/TLS establishment");
                    Telemetry.EndTlsHandshake(tlsActivity, tlsStart, SimpleTcpTelemetryNames.OutcomeTimeout, new TimeoutException("Timeout during SSL/TLS establishment", e));
                }
                else
                {
                    Logger?.Invoke($"{_header}client {client.IpPort} SSL/TLS exception: {Environment.NewLine}{e}");
                    Telemetry.EndTlsHandshake(tlsActivity, tlsStart, SimpleTcpTelemetryNames.OutcomeError, e);
                }

                client.Dispose();
                return false;
            }

            return true;
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // return true; // Allow untrusted certificates.
            return _settings.AcceptInvalidCertificates;
        }

        private async Task DataReceiver(ClientMetadata client)
        {
            InstanceTelemetry.DetachAmbientSpan();

            string ipPort = client.IpPort;
            Exception terminalException = null;
            Logger?.Invoke($"{_header}data receiver started for client {ipPort}");

            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_token, client.Token))
            {
                while (true)
                {
                    try
                    {
                        if (client.Token.IsCancellationRequested)
                        {
                            Logger?.Invoke($"{_header}cancellation requested (data receiver for client {ipPort})");
                            break;
                        }

                        var data = await DataReadAsync(client, linkedCts.Token).ConfigureAwait(false);
                        if (data.Array == null)
                        {
                            if (!IsClientConnected(client))
                            {
                                Logger?.Invoke($"{_header}client {ipPort} disconnected");
                                break;
                            }

                            if (client.Token.IsCancellationRequested)
                            {
                                Logger?.Invoke($"{_header}cancellation requested (data receiver for client {ipPort})");
                                break;
                            }

                            await Task.Delay(10, linkedCts.Token).ConfigureAwait(false);
                            continue;
                        }

                        Telemetry.SegmentReceived(data.Count);
                        QueueDataReceived(ipPort, data);
                        _statistics.AddReceivedBytes(data.Count);
                        client.UpdateLastSeen(MonotonicTime.GetTimestamp());
                    }
                    catch (IOException)
                    {
                        Logger?.Invoke($"{_header}data receiver canceled, peer disconnected [{ipPort}]");
                        break;
                    }
                    catch (SocketException)
                    {
                        Logger?.Invoke($"{_header}data receiver canceled, peer disconnected [{ipPort}]");
                        break;
                    }
                    catch (TaskCanceledException)
                    {
                        Logger?.Invoke($"{_header}data receiver task canceled [{ipPort}]");
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        Logger?.Invoke($"{_header}data receiver operation canceled [{ipPort}]");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        Logger?.Invoke($"{_header}data receiver canceled due to disposal [{ipPort}]");
                        break;
                    }
                    catch (Exception e)
                    {
                        Logger?.Invoke($"{_header}data receiver exception [{ipPort}]:{ Environment.NewLine}{e}{Environment.NewLine}");
                        terminalException = e;
                        break;
                    }
                }
            }

            Logger?.Invoke($"{_header}data receiver terminated for client {ipPort}");

            DisconnectReason reason = client.DisconnectReason;
            if (reason == DisconnectReason.None)
            {
                reason = DisconnectReason.Normal;
            }

            RecordDisconnect(ipPort, reason, client.ConnectedTimestamp, terminalException);

            _events.HandleClientDisconnected(this, new ConnectionEventArgs(ipPort, reason));

            _clients.TryRemove(ipPort, out _);
            if (client != null) client.Dispose();
        }

        private void RecordDisconnect(string ipPort, DisconnectReason reason, long connectedTimestamp, Exception terminalException)
        {
            double durationSeconds = InstanceTelemetry.ElapsedSeconds(connectedTimestamp);

            Activity activity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanDisconnect, ActivityKind.Internal);
            InstanceTelemetry.SetPeer(activity, ipPort);
            InstanceTelemetry.SetTag(activity, SimpleTcpTelemetryNames.AttributeReason, InstanceTelemetry.ReasonValue(reason));
            InstanceTelemetry.SetTag(activity, SimpleTcpTelemetryNames.AttributeConnectionDuration, durationSeconds);

            if (terminalException != null)
            {
                InstanceTelemetry.SetException(activity, SimpleTcpTelemetryNames.OutcomeError, terminalException);
                Telemetry.Error(SimpleTcpTelemetryNames.OperationReceive, terminalException);
            }
            else
            {
                InstanceTelemetry.SetOutcome(activity, SimpleTcpTelemetryNames.OutcomeSuccess);
            }

            Telemetry.ConnectionClosed(reason, durationSeconds);
            InstanceTelemetry.Stop(activity);
        }
           
        private async Task<ArraySegment<byte>> DataReadAsync(ClientMetadata client, CancellationToken token)
        {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_settings.StreamBufferSize);
            try
            {
#else
                byte[] buffer = new byte[_settings.StreamBufferSize];
#endif
                int read = !_ssl
                    ? await client.NetworkStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)
                    : await client.SslStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                if (read <= 0)
                {
                    throw new SocketException();
                }

                byte[] payload = new byte[read];
                Buffer.BlockCopy(buffer, 0, payload, 0, read);
                return new ArraySegment<byte>(payload, 0, read);
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
#endif
        }

        private async Task IdleClientMonitor()
        {
            InstanceTelemetry.DetachAmbientSpan();

            while (!_token.IsCancellationRequested)
            { 
                try
                {
                    await Task.Delay(_settings.IdleClientEvaluationIntervalMs, _token).ConfigureAwait(false);

                    if (_settings.IdleClientTimeoutMs == 0)
                    {
                        Telemetry.MonitorRun(SimpleTcpTelemetryNames.MonitorIdleClient, null);
                        continue;
                    }

                    long now = MonotonicTime.GetTimestamp();
                    foreach (ClientMetadata client in _clients.Values)
                    {
                        if (client == null) continue;
                        if (now - client.LastSeenTimestamp < MonotonicTime.FromMilliseconds(_settings.IdleClientTimeoutMs))
                        {
                            continue;
                        }

                        if (client.TryMarkDisconnectReason(DisconnectReason.Timeout))
                        {
                            Logger?.Invoke($"{_header}disconnecting {client.IpPort} due to timeout");
                            DisconnectClientInternal(client);
                        }
                    }

                    Telemetry.MonitorRun(SimpleTcpTelemetryNames.MonitorIdleClient, null);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Logger?.Invoke($"{_header}monitor exception: {e}");
                    Telemetry.MonitorRun(SimpleTcpTelemetryNames.MonitorIdleClient, e);
                }
            }
        }

        private void SendInternal(string ipPort, byte[] data)
        {
            Activity activity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanSend, ActivityKind.Client);
            InstanceTelemetry.SetPeer(activity, ipPort);
            long start = InstanceTelemetry.Timestamp();

            if (!_clients.TryGetValue(ipPort, out ClientMetadata client) || client == null)
            {
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeNotFound, null, 0);
                return;
            }

            try
            {
                client.SendLock.Wait();
                Telemetry.SendLockWaited(InstanceTelemetry.ElapsedSeconds(start));

                if (!_ssl) client.NetworkStream.Write(data, 0, data.Length);
                else client.SslStream.Write(data, 0, data.Length);

                if (!_ssl) client.NetworkStream.Flush();
                else client.SslStream.Flush();

                _statistics.AddSentBytes(data.Length);
                Telemetry.BytesSent(data.Length);
                _events.HandleDataSent(this, new DataSentEventArgs(ipPort, data.Length));
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeSuccess, null, data.Length);
            }
            catch (Exception e)
            {
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeError, e, data.Length);
                throw;
            }
            finally
            {
                client.SendLock.Release();
            }
        }

        private async Task SendInternalAsync(string ipPort, byte[] data, CancellationToken token)
        {
            ClientMetadata client = null;
            bool sendLockHeld = false;
            Activity activity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanSend, ActivityKind.Client);
            InstanceTelemetry.SetPeer(activity, ipPort);
            long start = InstanceTelemetry.Timestamp();

            try
            {
                if (!_clients.TryGetValue(ipPort, out client) || client == null)
                {
                    Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeNotFound, null, 0);
                    return;
                }

                await client.SendLock.WaitAsync(token).ConfigureAwait(false);
                sendLockHeld = true;
                Telemetry.SendLockWaited(InstanceTelemetry.ElapsedSeconds(start));

                if (!_ssl) await client.NetworkStream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                else await client.SslStream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);

                if (!_ssl) await client.NetworkStream.FlushAsync(token).ConfigureAwait(false);
                else await client.SslStream.FlushAsync(token).ConfigureAwait(false);

                _statistics.AddSentBytes(data.Length);
                Telemetry.BytesSent(data.Length);
                _events.HandleDataSent(this, new DataSentEventArgs(ipPort, data.Length));
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeSuccess, null, data.Length);
            }
            catch (OperationCanceledException)
            {
                // Includes TaskCanceledException.  Cancellation is reported, not thrown, to preserve existing behavior.
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeCanceled, null, data.Length);
            }
            catch (Exception e)
            {
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeError, e, data.Length);
                throw;
            }
            finally
            {
                if (client != null && sendLockHeld) client.SendLock.Release();
            }
        }

        private void SendInternal(string ipPort, long contentLength, Stream stream)
        {
            Activity activity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanSend, ActivityKind.Client);
            InstanceTelemetry.SetPeer(activity, ipPort);
            long start = InstanceTelemetry.Timestamp();

            if (!_clients.TryGetValue(ipPort, out ClientMetadata client) || client == null)
            {
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeNotFound, null, 0);
                return;
            }

            long bytesRemaining = contentLength;
            int bytesRead = 0;
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_settings.StreamBufferSize);
#else
            byte[] buffer = new byte[_settings.StreamBufferSize];
#endif
            try
            {
                client.SendLock.Wait();
                Telemetry.SendLockWaited(InstanceTelemetry.ElapsedSeconds(start));

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    if (!_ssl) client.NetworkStream.Write(buffer, 0, bytesRead); 
                    else client.SslStream.Write(buffer, 0, bytesRead); 

                    bytesRemaining -= bytesRead;
                    _statistics.AddSentBytes(bytesRead);
                    Telemetry.BytesSent(bytesRead);
                }

                if (!_ssl) client.NetworkStream.Flush();
                else client.SslStream.Flush();
                _events.HandleDataSent(this, new DataSentEventArgs(ipPort, contentLength));
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeSuccess, null, contentLength - bytesRemaining);
            }
            catch (Exception e)
            {
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeError, e, contentLength - bytesRemaining);
                throw;
            }
            finally
            {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                ArrayPool<byte>.Shared.Return(buffer);
#endif
                client.SendLock.Release();
            }
        }

        private async Task SendInternalAsync(string ipPort, long contentLength, Stream stream, CancellationToken token)
        {
            ClientMetadata client = null;
            bool sendLockHeld = false;
            long bytesRemaining = contentLength;
            Activity activity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanSend, ActivityKind.Client);
            InstanceTelemetry.SetPeer(activity, ipPort);
            long start = InstanceTelemetry.Timestamp();
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_settings.StreamBufferSize);
#else
            byte[] buffer = new byte[_settings.StreamBufferSize];
#endif
            try
            {
                if (!_clients.TryGetValue(ipPort, out client) || client == null)
                {
                    Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeNotFound, null, 0);
                    return;
                }

                int bytesRead = 0;

                await client.SendLock.WaitAsync(token).ConfigureAwait(false);
                sendLockHeld = true;
                Telemetry.SendLockWaited(InstanceTelemetry.ElapsedSeconds(start));

                while (bytesRemaining > 0)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    if (!_ssl) await client.NetworkStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                    else await client.SslStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);

                    bytesRemaining -= bytesRead;
                    _statistics.AddSentBytes(bytesRead);
                    Telemetry.BytesSent(bytesRead);
                }

                if (!_ssl) await client.NetworkStream.FlushAsync(token).ConfigureAwait(false);
                else await client.SslStream.FlushAsync(token).ConfigureAwait(false);
                _events.HandleDataSent(this, new DataSentEventArgs(ipPort, contentLength));
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeSuccess, null, contentLength - bytesRemaining);
            }
            catch (OperationCanceledException)
            {
                // Includes TaskCanceledException.  Cancellation is reported, not thrown, to preserve existing behavior.
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeCanceled, null, contentLength - bytesRemaining);
            }
            catch (Exception e)
            {
                Telemetry.EndSend(activity, start, SimpleTcpTelemetryNames.OutcomeError, e, contentLength - bytesRemaining);
                throw;
            }
            finally
            {
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                ArrayPool<byte>.Shared.Return(buffer);
#endif
                if (client != null && sendLockHeld) client.SendLock.Release();
            }
        }

        private void EnsureAsyncDataReceivedDispatcher()
        {
            if (_asyncDataReceivedDispatcher != null)
            {
                return;
            }

            // A single worker guarantees that DataReceived handlers execute one-at-a-time and in
            // the exact order segments were read from the socket, while still decoupling handler
            // execution from the receive loop. Using multiple workers would allow a later segment
            // to be handled before (or concurrently with) an earlier one, corrupting message
            // reassembly for stream-oriented consumers. See issue #236.
            const int workerCount = 1;
            _asyncDataReceivedDispatcher = new AsyncEventDispatcher<DataReceivedWorkItem>(
                item =>
                {
                    Telemetry.DispatchDequeued(InstanceTelemetry.ElapsedSeconds(item.EnqueuedTimestamp));
                    InvokeDataReceived(item.Args, item.ParentContext, SimpleTcpTelemetryNames.DispatchAsync);
                },
                workerCount,
                e => Logger?.Invoke($"{_header}DataReceived handler exception: {e}"));
        }

        private void QueueDataReceived(string ipPort, ArraySegment<byte> data)
        {
            var args = new DataReceivedEventArgs(ipPort, data);

            Activity receiveActivity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanReceive, ActivityKind.Consumer);
            InstanceTelemetry.SetPeer(receiveActivity, ipPort);
            InstanceTelemetry.SetTag(receiveActivity, SimpleTcpTelemetryNames.AttributeBytes, data.Count);
            ActivityContext parentContext = receiveActivity != null ? receiveActivity.Context : default(ActivityContext);

            try
            {
                if (_settings.UseAsyncDataReceivedEvents)
                {
                    EnsureAsyncDataReceivedDispatcher();
                    Telemetry.DispatchEnqueued();
                    _asyncDataReceivedDispatcher.Enqueue(new DataReceivedWorkItem(args, parentContext, InstanceTelemetry.Timestamp()));
                    InstanceTelemetry.SetOutcome(receiveActivity, SimpleTcpTelemetryNames.OutcomeSuccess);
                    return;
                }

                InvokeDataReceived(args, parentContext, SimpleTcpTelemetryNames.DispatchSync);
                InstanceTelemetry.SetOutcome(receiveActivity, SimpleTcpTelemetryNames.OutcomeSuccess);
            }
            catch (Exception e)
            {
                InstanceTelemetry.SetException(receiveActivity, SimpleTcpTelemetryNames.OutcomeError, e);
                throw;
            }
            finally
            {
                InstanceTelemetry.Stop(receiveActivity);
            }
        }

        private void InvokeDataReceived(DataReceivedEventArgs args, ActivityContext parentContext, string dispatchMode)
        {
            Activity processActivity = Telemetry.StartActivity(SimpleTcpTelemetryNames.SpanProcess, ActivityKind.Internal, parentContext);
            InstanceTelemetry.SetPeer(processActivity, args.IpPort);
            InstanceTelemetry.SetTag(processActivity, SimpleTcpTelemetryNames.AttributeDispatchMode, dispatchMode);
            long start = InstanceTelemetry.Timestamp();

            try
            {
                _events.HandleDataReceived(this, args);
                InstanceTelemetry.SetOutcome(processActivity, SimpleTcpTelemetryNames.OutcomeSuccess);
                Telemetry.HandlerCompleted(dispatchMode, SimpleTcpTelemetryNames.OutcomeSuccess, InstanceTelemetry.ElapsedSeconds(start), null);
            }
            catch (Exception e)
            {
                InstanceTelemetry.SetException(processActivity, SimpleTcpTelemetryNames.OutcomeError, e);
                Telemetry.HandlerCompleted(dispatchMode, SimpleTcpTelemetryNames.OutcomeError, InstanceTelemetry.ElapsedSeconds(start), e);
                throw;
            }
            finally
            {
                InstanceTelemetry.Stop(processActivity);
            }
        }

        private void DisconnectClientInternal(ClientMetadata client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                if (!client.TokenSource.IsCancellationRequested)
                {
                    client.TokenSource.Cancel();
                }
            }
            catch
            {
            }

            try
            {
                if (client.Client?.Client != null && client.Client.Client.Connected)
                {
                    client.Client.Client.Shutdown(SocketShutdown.Both);
                }
            }
            catch
            {
            }

            try
            {
                client.Dispose();
            }
            catch
            {
            }
        }

        private bool IsClientPermitted(string clientIp)
        {
            if (_settings.PermittedIPs == null || _settings.PermittedIPs.Count < 1)
            {
                return true;
            }

            return GetOrCreateIpLookup(
                _settings.PermittedIPs,
                ref _permittedIpLookup,
                ref _permittedIpSource,
                ref _permittedIpLookupCount).Contains(clientIp);
        }

        private bool IsClientBlocked(string clientIp)
        {
            if (_settings.BlockedIPs == null || _settings.BlockedIPs.Count < 1)
            {
                return false;
            }

            return GetOrCreateIpLookup(
                _settings.BlockedIPs,
                ref _blockedIpLookup,
                ref _blockedIpSource,
                ref _blockedIpLookupCount).Contains(clientIp);
        }

        private HashSet<string> GetOrCreateIpLookup(
            List<string> source,
            ref HashSet<string> lookup,
            ref List<string> sourceReference,
            ref int sourceCount)
        {
            if (lookup == null || !ReferenceEquals(sourceReference, source) || sourceCount != source.Count)
            {
                lookup = new HashSet<string>(source, StringComparer.OrdinalIgnoreCase);
                sourceReference = source;
                sourceCount = source.Count;
            }

            return lookup;
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
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void EnableKeepalives()
        {
            // issues with definitions: https://github.com/dotnet/sdk/issues/14540

            try
            {
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER

                // NETCOREAPP3_1_OR_GREATER catches .NET 5.0

                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _keepalive.TcpKeepAliveTime);
                _listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _keepalive.TcpKeepAliveInterval);
                _listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _keepalive.TcpKeepAliveRetryCount);

#elif NETFRAMEWORK

            byte[] keepAlive = new byte[12];

            // Turn keepalive on
            Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);

            // Set TCP keepalive time
            Buffer.BlockCopy(BitConverter.GetBytes((uint)_keepalive.TcpKeepAliveTimeMilliseconds), 0, keepAlive, 4, 4);

            // Set TCP keepalive interval
            Buffer.BlockCopy(BitConverter.GetBytes((uint)_keepalive.TcpKeepAliveIntervalMilliseconds), 0, keepAlive, 8, 4);

            // Set keepalive settings on the underlying Socket
            _listener.Server.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception e)
            {
                Logger?.Invoke($"{_header}keepalives not supported on this platform, disabled");
                Telemetry.Error(SimpleTcpTelemetryNames.OperationKeepalive, e);
            }
        }

        private void EnableKeepalives(TcpClient client)
        {
            try
            {
#if NETCOREAPP3_1_OR_GREATER || NET6_0_OR_GREATER

                // NETCOREAPP3_1_OR_GREATER catches .NET 5.0

                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _keepalive.TcpKeepAliveTime);
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _keepalive.TcpKeepAliveInterval);

                // Windows 10 version 1703 or later

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    && Environment.OSVersion.Version >= new Version(10, 0, 15063))
                {
                    client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _keepalive.TcpKeepAliveRetryCount);
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
                client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception e)
            {
                Logger?.Invoke($"{_header}keepalives not supported on this platform, disabled");
                Telemetry.Error(SimpleTcpTelemetryNames.OperationKeepalive, e);
                _keepalive.EnableTcpKeepAlives = false;
            }
        }

        #endregion
    }
}
