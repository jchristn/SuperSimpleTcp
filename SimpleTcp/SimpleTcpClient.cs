using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                return _Connected;
            }
            private set
            {
                _Connected = value;
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

        #endregion

        #region Private-Members

        private string _Header = "[SimpleTcp.Client] ";
        private SimpleTcpClientSettings _Settings = new SimpleTcpClientSettings();
        private SimpleTcpClientEvents _Events = new SimpleTcpClientEvents();
        private SimpleTcpKeepaliveSettings _Keepalive = new SimpleTcpKeepaliveSettings();
        private SimpleTcpStatistics _Statistics = new SimpleTcpStatistics();

        private string _ServerIp;
        private IPAddress _IPAddress;
        private int _Port;
        private System.Net.Sockets.TcpClient _Client;
        private NetworkStream _NetworkStream;

        private bool _Ssl;
        private string _PfxCertFilename;
        private string _PfxPassword;
        private SslStream _SslStream;
        private X509Certificate2 _SslCert;
        private X509Certificate2Collection _SslCertCollection;

        private SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1); 
        private bool _Connected = false;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
         
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP client.  Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(string ipPort, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _ServerIp, out _Port);
            if (_Port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (String.IsNullOrEmpty(_ServerIp)) throw new ArgumentNullException("Server IP or hostname must not be null.");

            _Token = _TokenSource.Token; 

            if (!IPAddress.TryParse(_ServerIp, out _IPAddress))
            {
                _IPAddress = Dns.GetHostEntry(_ServerIp).AddressList[0];
            }

            InitializeClient(ssl, pfxCertFilename, pfxPassword);
        }

        /// <summary>
        /// Instantiates the TCP client.  Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpClient(string serverIpOrHostname, int port, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (String.IsNullOrEmpty(serverIpOrHostname)) throw new ArgumentNullException(nameof(serverIpOrHostname));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _Token = _TokenSource.Token;
            _ServerIp = serverIpOrHostname;
            _Port = port;

            if (!IPAddress.TryParse(_ServerIp, out _IPAddress))
            {
                _IPAddress = Dns.GetHostEntry(serverIpOrHostname).AddressList[0];
            }

            InitializeClient(ssl, pfxCertFilename, pfxPassword); 
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
        /// Establish the connection to the server.
        /// </summary>
        public void Connect()
        {
            Logger?.Invoke(_Header + "Connecting to " + _ServerIp + ":" + _Port);

            if (_Keepalive.EnableTcpKeepAlives) EnableKeepalives();

            IAsyncResult ar = _Client.BeginConnect(_ServerIp, _Port, null, null);
            WaitHandle wh = ar.AsyncWaitHandle;

            try
            {
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(_Settings.ConnectTimeoutSeconds), false))
                {
                    _Client.Close();
                    throw new TimeoutException("Timeout connecting to " + _ServerIp + ":" + _Port);
                }

                _Client.EndConnect(ar); 
                _NetworkStream = _Client.GetStream();

                if (_Ssl)
                {
                    if (_Settings.AcceptInvalidCertificates)
                    {
                        // accept invalid certs
                        _SslStream = new SslStream(_NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                    }
                    else
                    {
                        // do not accept invalid SSL certificates
                        _SslStream = new SslStream(_NetworkStream, false);
                    }

                    _SslStream.AuthenticateAsClient(_ServerIp, _SslCertCollection, SslProtocols.Tls12, !_Settings.AcceptInvalidCertificates);

                    if (!_SslStream.IsEncrypted)
                    {
                        throw new AuthenticationException("Stream is not encrypted");
                    }

                    if (!_SslStream.IsAuthenticated)
                    {
                        throw new AuthenticationException("Stream is not authenticated");
                    }

                    if (_Settings.MutuallyAuthenticate && !_SslStream.IsMutuallyAuthenticated)
                    {
                        throw new AuthenticationException("Mutual authentication failed");
                    }
                } 

                _Connected = true;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                wh.Close();
            }

            _Events.HandleConnected(this);

            Task.Run(() => DataReceiver(_Token), _Token);
        }

        /// <summary>
        /// Send data to the server.
        /// </summary>
        /// <param name="data">String containing data to send.</param>
        public void Send(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (!_Connected) throw new IOException("Not connected to the server; use Connect() first.");
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(bytes.Length, ms);
        }

        /// <summary>
        /// Send data to the server.
        /// </summary> 
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (!_Connected) throw new IOException("Not connected to the server; use Connect() first.");
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(data.Length, ms); 
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
            if (!_Connected) throw new IOException("Not connected to the server; use Connect() first.");
            SendInternal(contentLength, stream);
        }

        /// <summary>
        /// Send data to the server asynchronously.
        /// </summary>
        /// <param name="data">String containing data to send.</param>
        public async Task SendAsync(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (!_Connected) throw new IOException("Not connected to the server; use Connect() first.");
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(bytes.Length, ms);
        }

        /// <summary>
        /// Send data to the server asynchronously.
        /// </summary> 
        /// <param name="data">Byte array containing data to send.</param>
        public async Task SendAsync(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (!_Connected) throw new IOException("Not connected to the server; use Connect() first.");
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(data.Length, ms);
        }

        /// <summary>
        /// Send data to the server asynchronously.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the source stream to send.</param>
        /// <param name="stream">Stream containing the data to send.</param>
        public async Task SendAsync(long contentLength, Stream stream)
        { 
            if (contentLength < 1) return;
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
            if (!_Connected) throw new IOException("Not connected to the server; use Connect() first.");
            await SendInternalAsync(contentLength, stream);
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
                _Connected = false;

                if (_TokenSource != null)
                {
                    if (!_TokenSource.IsCancellationRequested) _TokenSource.Cancel();
                    _TokenSource.Dispose();
                    _TokenSource = null;
                }

                if (_SslStream != null)
                {
                    _SslStream.Close();
                    _SslStream.Dispose();
                    _SslStream = null;
                }

                if (_NetworkStream != null)
                {
                    _NetworkStream.Close();
                    _NetworkStream.Dispose();
                    _NetworkStream = null;
                }

                if (_Client != null)
                {
                    _Client.Close();
                    _Client.Dispose();
                    _Client = null;
                }

                Logger?.Invoke(_Header + "Dispose complete");
            }
        }

        private void InitializeClient(bool ssl, string pfxCertFilename, string pfxPassword)
        {
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
            _Client = new System.Net.Sockets.TcpClient();
            _SslStream = null;
            _SslCert = null;
            _SslCertCollection = null;

            if (_Ssl)
            {
                if (String.IsNullOrEmpty(pfxPassword))
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
                        Logger?.Invoke(_Header + "Disconnection detected");
                        break;
                    }
                     
                    byte[] data = await DataReadAsync(token);
                    if (data == null)
                    { 
                        await Task.Delay(30);
                        continue;
                    }

                    _Events.HandleDataReceived(this, new DataReceivedFromServerEventArgs(data));
                    _Statistics.ReceivedBytes += data.Length;
                } 
            }
            catch (ObjectDisposedException)
            {

            }
            catch (SocketException)
            {
                Logger?.Invoke(_Header + "Data receiver socket exception (disconnection)");
            }
            catch (Exception e)
            {
                Logger?.Invoke(_Header + "Data receiver exception:" + 
                    Environment.NewLine + 
                    e.ToString() + 
                    Environment.NewLine);
            }

            _Connected = false;
            _Events.HandleDisconnected(this);
        }

        private async Task<byte[]> DataReadAsync(CancellationToken token)
        { 
            if (_Client == null 
                || !_Client.Connected
                || token.IsCancellationRequested) 
                throw new OperationCanceledException();

            if (!_NetworkStream.CanRead)
                throw new IOException();

            if (_Ssl && !_SslStream.CanRead)
                throw new IOException();
             
            byte[] buffer = new byte[_Settings.StreamBufferSize];
            int read = 0;

            if (!_Ssl)
            { 
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        read = await _NetworkStream.ReadAsync(buffer, 0, buffer.Length);

                        if (read > 0)
                        {
                            ms.Write(buffer, 0, read);
                            return ms.ToArray();
                        }
                        else
                        {
                            throw new SocketException();
                        }
                    }
                } 
            }
            else
            { 
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        read = await _SslStream.ReadAsync(buffer, 0, buffer.Length);

                        if (read > 0)
                        {
                            ms.Write(buffer, 0, read);
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

        private async Task SendInternalAsync(long contentLength, Stream stream)
        {
            long bytesRemaining = contentLength;
            int bytesRead = 0;
            byte[] buffer = new byte[_Settings.StreamBufferSize];

            try
            {
                await _SendLock.WaitAsync();

                while (bytesRemaining > 0)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        if (!_Ssl) await _NetworkStream.WriteAsync(buffer, 0, bytesRead);
                        else await _SslStream.WriteAsync(buffer, 0, bytesRead);

                        bytesRemaining -= bytesRead;
                        _Statistics.SentBytes += bytesRead;
                    }
                }

                if (!_Ssl) await _NetworkStream.FlushAsync();
                else await _SslStream.FlushAsync();
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
#if NETCOREAPP

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
                Logger?.Invoke(_Header + "Keepalives not supported on this platform, disabled");
            }
        }

        #endregion
    }
}
