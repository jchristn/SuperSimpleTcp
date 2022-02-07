using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleTcp
{
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
                return _IsConnected;
            }
            private set
            {
                _IsConnected = value;
            }
        }

        /// <summary>
        /// Client IPEndPoint if connected.
        /// </summary>
        public IPEndPoint LocalEndpoint
        {
            get
            {
                if (_Client != null && _IsConnected)
                {
                    return (IPEndPoint)_Client.Client.LocalEndPoint;
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
                return _Settings;
            }
            set
            {
                if (value == null) _Settings = new SimpleTcpClientSettings();
                else _Settings = value;
            }
        }

        /// <summary>
        /// SimpleTcp client events.
        /// </summary>
        public SimpleTcpClientEvents Events
        {
            get
            {
                return _Events;
            }
            set
            {
                if (value == null) _Events = new SimpleTcpClientEvents();
                else _Events = value;
            }
        }

        /// <summary>
        /// SimpleTcp statistics.
        /// </summary>
        public SimpleTcpStatistics Statistics
        {
            get
            {
                return _Statistics;
            }
        }

        /// <summary>
        /// SimpleTcp keepalive settings.
        /// </summary>
        public SimpleTcpKeepaliveSettings Keepalive
        {
            get
            {
                return _Keepalive;
            }
            set
            {
                if (value == null) _Keepalive = new SimpleTcpKeepaliveSettings();
                else _Keepalive = value;
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
                return $"{_ServerIp}:{_ServerPort}";
            }
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[SimpleTcp.Client] ";
        private SimpleTcpClientSettings _Settings = new SimpleTcpClientSettings();
        private SimpleTcpClientEvents _Events = new SimpleTcpClientEvents();
        private SimpleTcpKeepaliveSettings _Keepalive = new SimpleTcpKeepaliveSettings();
        private SimpleTcpStatistics _Statistics = new SimpleTcpStatistics();

        private string _ServerIp = null;
        private int _ServerPort = 0;
        private readonly IPAddress _IPAddress = null;
        private TcpClient _Client = null;
        private NetworkStream _NetworkStream = null;

        private bool _Ssl = false;
        private string _PfxCertFilename = null;
        private string _PfxPassword = null;
        private SslStream _SslStream = null;
        private X509Certificate2 _SslCert = null;
        private X509Certificate2Collection _SslCertCollection = null;

        private readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);
        private bool _IsConnected = false;

        private Task _DataReceiver = null;
        private Task _IdleServerMonitor = null;
        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;

        private DateTime _LastActivity = DateTime.Now;
        private bool _IsTimeout = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP client without SSL. Set the Connected, Disconnected, and DataReceived callbacks. Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        public SimpleTcpClient(string ipPort)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _ServerIp, out _ServerPort);
            if (_ServerPort < 0) throw new ArgumentException("Port must be zero or greater.");
            if (string.IsNullOrEmpty(_ServerIp)) throw new ArgumentNullException("Server IP or hostname must not be null.");

            if (!IPAddress.TryParse(_ServerIp, out _IPAddress))
            {
                _IPAddress = Dns.GetHostEntry(_ServerIp).AddressList[0];
                _ServerIp = _IPAddress.ToString();
            }
        }

        /// <summary>
        /// Instantiates the TCP client. Set the Connected, Disconnected, and DataReceived callbacks. Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(string ipPort, bool ssl, string pfxCertFilename, string pfxPassword) : this(ipPort)
        {
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
        }

        /// <summary>
        /// Instantiates the TCP client without SSL. Set the Connected, Disconnected, and DataReceived callbacks. Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        public SimpleTcpClient(string serverIpOrHostname, int port)
        {
            if (string.IsNullOrEmpty(serverIpOrHostname)) throw new ArgumentNullException(nameof(serverIpOrHostname));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _ServerIp = serverIpOrHostname;
            _ServerPort = port;

            if (!IPAddress.TryParse(_ServerIp, out _IPAddress))
            {
                _IPAddress = Dns.GetHostEntry(serverIpOrHostname).AddressList[0];
                _ServerIp = _IPAddress.ToString();
            } 
        }

        /// <summary>
        /// Instantiates the TCP client.  Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(string serverIpOrHostname, int port, bool ssl, string pfxCertFilename, string pfxPassword) : this(serverIpOrHostname, port)
        {
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
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
                Logger?.Invoke($"{_Header}already connected");
                return;
            }
            else
            {
                Logger?.Invoke($"{_Header}initializing client");
                InitializeClient(_Ssl, _PfxCertFilename, _PfxPassword);
                Logger?.Invoke($"{_Header}connecting to {ServerIpPort}");
            }

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;

            IAsyncResult ar = _Client.BeginConnect(_ServerIp, _ServerPort, null, null);
            WaitHandle wh = ar.AsyncWaitHandle;

            try
            {
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(_Settings.ConnectTimeoutMs), false))
                {
                    _Client.Close();
                    throw new TimeoutException($"Timeout connecting to {ServerIpPort}");
                }

                _Client.EndConnect(ar);
                _NetworkStream = _Client.GetStream();

                if (_Ssl)
                {
                    if (_Settings.AcceptInvalidCertificates)
                        _SslStream = new SslStream(_NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                    else
                        _SslStream = new SslStream(_NetworkStream, false);

                    _SslStream.AuthenticateAsClient(_ServerIp, _SslCertCollection, SslProtocols.Tls12, !_Settings.AcceptInvalidCertificates);

                    if (!_SslStream.IsEncrypted) throw new AuthenticationException("Stream is not encrypted");
                    if (!_SslStream.IsAuthenticated) throw new AuthenticationException("Stream is not authenticated");
                    if (_Settings.MutuallyAuthenticate && !_SslStream.IsMutuallyAuthenticated) throw new AuthenticationException("Mutual authentication failed");
                }

                if (_Keepalive.EnableTcpKeepAlives) EnableKeepalives();
            }
            catch (Exception)
            {
                throw;
            }

            _IsConnected = true;
            _LastActivity = DateTime.Now;
            _IsTimeout = false;
            _Events.HandleConnected(this, new ConnectionEventArgs(ServerIpPort));
            _DataReceiver = Task.Run(() => DataReceiver(_Token), _Token);
            _IdleServerMonitor = Task.Run(() => IdleServerMonitor(), _Token);
        }

        /// <summary>
        /// Establish the connection to the server with retries up to either the timeout specified or the value in Settings.ConnectTimeoutMs.
        /// </summary>
        /// <param name="timeoutMs">The amount of time in milliseconds to continue attempting connections.</param>
        public void ConnectWithRetries(int? timeoutMs = null)
        {
            if (timeoutMs != null && timeoutMs < 1) throw new ArgumentException("Timeout milliseconds must be greater than zero.");
            if (timeoutMs != null) _Settings.ConnectTimeoutMs = timeoutMs.Value;

            if (IsConnected)
            {
                Logger?.Invoke($"{_Header}already connected");
                return;
            }
            else
            {
                Logger?.Invoke($"{_Header}initializing client");

                InitializeClient(_Ssl, _PfxCertFilename, _PfxPassword);

                Logger?.Invoke($"{_Header}connecting to {ServerIpPort}");
            }

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;

            using (CancellationTokenSource connectTokenSource = new CancellationTokenSource())
            {
                CancellationToken connectToken = connectTokenSource.Token;

                Task cancelTask = Task.Delay(_Settings.ConnectTimeoutMs, _Token);
                Task connectTask = Task.Run(() =>
                {
                    int retryCount = 0;

                    while (true)
                    {
                        try
                        {
                            string msg = $"{_Header}attempting connection to {_ServerIp}:{_ServerPort}";
                            if (retryCount > 0) msg += $" ({retryCount} retries)";
                            Logger?.Invoke(msg);

                            _Client.Dispose();
                            _Client = new TcpClient();
                            _Client.ConnectAsync(_ServerIp, _ServerPort).Wait(1000, connectToken);

                            if (_Client.Connected)
                            {
                                Logger?.Invoke($"{_Header}connected to {_ServerIp}:{_ServerPort}");
                                break;
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
                        catch (Exception e)
                        {
                            Logger?.Invoke($"{_Header}failed connecting to {_ServerIp}:{_ServerPort}: {e.Message}");
                        }
                        finally
                        {
                            retryCount++;
                        }
                    }
                }, connectToken);

                Task.WhenAny(cancelTask, connectTask).Wait();

                if (cancelTask.IsCompleted)
                {
                    connectTokenSource.Cancel();
                    _Client.Close();
                    throw new TimeoutException($"Timeout connecting to {ServerIpPort}");
                }

                try
                {
                    _NetworkStream = _Client.GetStream();

                    if (_Ssl)
                    {
                        if (_Settings.AcceptInvalidCertificates)
                            _SslStream = new SslStream(_NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                        else
                            _SslStream = new SslStream(_NetworkStream, false);

                        _SslStream.AuthenticateAsClient(_ServerIp, _SslCertCollection, SslProtocols.Tls12, !_Settings.AcceptInvalidCertificates);

                        if (!_SslStream.IsEncrypted) throw new AuthenticationException("Stream is not encrypted");
                        if (!_SslStream.IsAuthenticated) throw new AuthenticationException("Stream is not authenticated");
                        if (_Settings.MutuallyAuthenticate && !_SslStream.IsMutuallyAuthenticated) throw new AuthenticationException("Mutual authentication failed");
                    }

                    if (_Keepalive.EnableTcpKeepAlives) EnableKeepalives();
                }
                catch (Exception)
                {
                    throw;
                }

            }

            _IsConnected = true;
            _LastActivity = DateTime.Now;
            _IsTimeout = false;
            _Events.HandleConnected(this, new ConnectionEventArgs(ServerIpPort));
            _DataReceiver = Task.Run(() => DataReceiver(_Token), _Token);
            _IdleServerMonitor = Task.Run(() => IdleServerMonitor(), _Token);
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                Logger?.Invoke($"{_Header}already disconnected");
                return;
            }
            else
            {
                Logger?.Invoke($"{_Header}disconnecting from {ServerIpPort}");
            }

            _TokenSource.Cancel();
            _TokenSource.Token.WaitHandle.WaitOne();

            _Client.Close();
            _IsConnected = false;
        }

        /// <summary>
        /// Send data to the server.
        /// </summary>
        /// <param name="data">String containing data to send.</param>
        public void Send(string data)
        {
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (!_IsConnected) throw new IOException("Not connected to the server; use Connect() first.");

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
            if (!_IsConnected) throw new IOException("Not connected to the server; use Connect() first.");

            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(data, 0, data.Length);
                ms.Seek(0, SeekOrigin.Begin);
                SendInternal(data.Length, ms);
            }
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
            if (!_IsConnected) throw new IOException("Not connected to the server; use Connect() first.");
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
            if (!_IsConnected) throw new IOException("Not connected to the server; use Connect() first.");
            if (token == default(CancellationToken)) token = _Token;
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(bytes.Length, ms, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the server asynchronously.
        /// </summary> 
        /// <param name="data">Byte array containing data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(byte[] data, CancellationToken token = default)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (!_IsConnected) throw new IOException("Not connected to the server; use Connect() first.");
            if (token == default(CancellationToken)) token = _Token;
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(data.Length, ms, token).ConfigureAwait(false);
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
            if (!_IsConnected) throw new IOException("Not connected to the server; use Connect() first.");
            if (token == default(CancellationToken)) token = _Token;
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
                _IsConnected = false;

                if (_TokenSource != null)
                {
                    if (!_TokenSource.IsCancellationRequested)
                    {
                        _TokenSource.Cancel();
                        _TokenSource.Dispose();
                    }
                }

                if (_SslStream != null)
                {
                    _SslStream.Close();
                    _SslStream.Dispose(); 
                }

                if (_NetworkStream != null)
                {
                    _NetworkStream.Close();
                    _NetworkStream.Dispose(); 
                }

                if (_Client != null)
                {
                    _Client.Close();
                    _Client.Dispose(); 
                }

                Logger?.Invoke($"{_Header}dispose complete");
            }
        }

        private void InitializeClient(bool ssl, string pfxCertFilename, string pfxPassword)
        {
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
            _Client = new TcpClient();
            _SslStream = null;
            _SslCert = null;
            _SslCertCollection = null;

            if (_Ssl)
            {
                if (string.IsNullOrEmpty(pfxPassword))
                {
                    _SslCert = new X509Certificate2(pfxCertFilename);
                }
                else
                {
                    _SslCert = new X509Certificate2(pfxCertFilename, pfxPassword);
                }

                _SslCertCollection = new X509Certificate2Collection
                {
                    _SslCert
                };
            }
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        { 
            return _Settings.AcceptInvalidCertificates;
        }

        private async Task DataReceiver(CancellationToken token)
        { 
            try
            { 
                while (true)
                {
                    if (token.IsCancellationRequested
                        || _Client == null 
                        || !_Client.Connected)
                    {
                        Logger?.Invoke($"{_Header}disconnection detected");
                        break;
                    }
                     
                    byte[] data = await DataReadAsync(token).ConfigureAwait(false);
                    if (data == null)
                    { 
                        await Task.Delay(10).ConfigureAwait(false);
                        continue;
                    }

                    _LastActivity = DateTime.Now;
                    _Events.HandleDataReceived(this, new DataReceivedEventArgs(ServerIpPort, data));
                    _Statistics.ReceivedBytes += data.Length;
                } 
            }
            catch (IOException)
            {
                Logger?.Invoke($"{_Header}data receiver canceled, peer disconnected");
            }
            catch (SocketException)
            {
                Logger?.Invoke($"{_Header}data receiver canceled, peer disconnected");
            }
            catch (TaskCanceledException)
            {
                Logger?.Invoke($"{_Header}data receiver task canceled");
            }
            catch (OperationCanceledException)
            {
                Logger?.Invoke($"{_Header}data receiver operation canceled");
            }
            catch (ObjectDisposedException)
            {
                Logger?.Invoke($"{_Header}data receiver canceled due to disposal");
            }
            catch (Exception e)
            {
                Logger?.Invoke($"{_Header}data receiver exception:{Environment.NewLine}{e}{Environment.NewLine}");
            }

            _IsConnected = false;

            if (!_IsTimeout) _Events.HandleClientDisconnected(this, new ConnectionEventArgs(ServerIpPort, DisconnectReason.Normal));
            else _Events.HandleClientDisconnected(this, new ConnectionEventArgs(ServerIpPort, DisconnectReason.Timeout));

            Dispose();
        }

        private async Task<byte[]> DataReadAsync(CancellationToken token)
        {  
            byte[] buffer = new byte[_Settings.StreamBufferSize];
            int read = 0;

            if (!_Ssl)
            { 
                using (MemoryStream ms = new MemoryStream())
                { 
                    read = await _NetworkStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                    if (read > 0)
                    {
                        await ms.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                        return ms.ToArray();
                    }
                    else
                    {
                        throw new SocketException();
                    } 
                } 
            }
            else
            { 
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        read = await _SslStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                        if (read > 0)
                        {
                            await ms.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                            return ms.ToArray();
                        }
                        else
                        {
                            throw new SocketException();
                        }
                    }
                } 
            } 
        }

        private void SendInternal(long contentLength, Stream stream)
        { 
            long bytesRemaining = contentLength;
            int bytesRead = 0;
            byte[] buffer = new byte[_Settings.StreamBufferSize];

            try
            {
                _SendLock.Wait();

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        if (!_Ssl) _NetworkStream.Write(buffer, 0, bytesRead);
                        else _SslStream.Write(buffer, 0, bytesRead);

                        bytesRemaining -= bytesRead;
                        _Statistics.SentBytes += bytesRead;
                    }
                }

                if (!_Ssl) _NetworkStream.Flush();
                else _SslStream.Flush();
            }
            finally
            {
                _SendLock.Release();
            }
        }

        private async Task SendInternalAsync(long contentLength, Stream stream, CancellationToken token)
        {
            try
            {
                long bytesRemaining = contentLength;
                int bytesRead = 0;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                await _SendLock.WaitAsync(token).ConfigureAwait(false);

                while (bytesRemaining > 0)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        if (!_Ssl) await _NetworkStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                        else await _SslStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);

                        bytesRemaining -= bytesRead;
                        _Statistics.SentBytes += bytesRead;
                    }
                }

                if (!_Ssl) await _NetworkStream.FlushAsync(token).ConfigureAwait(false);
                else await _SslStream.FlushAsync(token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                _SendLock.Release();
            }
        }

        private void EnableKeepalives()
        {
            try
            {
#if NETCOREAPP || NET5_0

                _Client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _Client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _Keepalive.TcpKeepAliveTime);
                _Client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _Keepalive.TcpKeepAliveInterval);
                _Client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _Keepalive.TcpKeepAliveRetryCount);

#elif NETFRAMEWORK

                byte[] keepAlive = new byte[12];

                // Turn keepalive on
                Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);

                // Set TCP keepalive time
                Buffer.BlockCopy(BitConverter.GetBytes((uint)_Keepalive.TcpKeepAliveTime), 0, keepAlive, 4, 4);

                // Set TCP keepalive interval
                Buffer.BlockCopy(BitConverter.GetBytes((uint)_Keepalive.TcpKeepAliveInterval), 0, keepAlive, 8, 4);

                // Set keepalive settings on the underlying Socket
                _Client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception)
            {
                Logger?.Invoke($"{_Header}keepalives not supported on this platform, disabled");
                _Keepalive.EnableTcpKeepAlives = false;
            }
        }

        private async Task IdleServerMonitor()
        {
            while (!_Token.IsCancellationRequested)
            {
                await Task.Delay(_Settings.IdleServerEvaluationIntervalMs, _Token).ConfigureAwait(false);

                if (_Settings.IdleServerTimeoutMs == 0) continue;

                DateTime timeoutTime = _LastActivity.AddMilliseconds(_Settings.IdleServerTimeoutMs);

                if (DateTime.Now > timeoutTime)
                {
                    Logger?.Invoke($"{_Header}disconnecting from {ServerIpPort} due to timeout");
                    _IsConnected = false;
                    _IsTimeout = true;
                    _TokenSource.Cancel(); // DataReceiver will fire events including dispose
                }
            }
        }

        #endregion
    }
}
