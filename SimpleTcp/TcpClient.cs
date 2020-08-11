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
    /// TCP client with SSL support.  
    /// Set the Connected, Disconnected, and DataReceived callbacks.  
    /// Once set, use Connect() to connect to the server.
    /// </summary>
    public class TcpClient : IDisposable
    {
        #region Public-Members
         
        /// <summary>
        /// Callback to call when the connection is established.
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Callback to call when the connection is destroyed.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Callback to call when byte data has become available from the server.
        /// </summary>
        public event EventHandler<DataReceivedFromServerEventArgs> DataReceived;

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
        /// The number of seconds to wait when attempting to connect.
        /// </summary>
        public int ConnectTimeoutSeconds
        {
            get
            {
                return _ConnectTimeoutSeconds;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ConnectTimeoutSeconds must be greater than zero.");
                _ConnectTimeoutSeconds = value;
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
        /// Access TCP statistics.
        /// </summary>
        public Statistics Stats
        {
            get
            {
                return _Stats;
            }
        }

        /// <summary>
        /// Method to invoke to send a log message.
        /// </summary>
        public Action<string> Logger = null;

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private int _ConnectTimeoutSeconds = 5;
        private string _ServerIp;
        private IPAddress _IPAddress;
        private int _Port;
        private System.Net.Sockets.TcpClient _TcpClient;
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

        private Statistics _Stats = new Statistics();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP client.  Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIpOrHostname">The server IP address or hostname.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public TcpClient(string serverIpOrHostname, int port, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (String.IsNullOrEmpty(serverIpOrHostname)) throw new ArgumentNullException(nameof(serverIpOrHostname));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
              
            _Token = _TokenSource.Token; 
            _ServerIp = serverIpOrHostname;

            if (!IPAddress.TryParse(_ServerIp, out _IPAddress))
            {
                _IPAddress = Dns.GetHostEntry(serverIpOrHostname).AddressList[0];
            }
            
            _Port = port;
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword; 
            _TcpClient = new System.Net.Sockets.TcpClient(); 
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
            IAsyncResult ar = _TcpClient.BeginConnect(_ServerIp, _Port, null, null);
            WaitHandle wh = ar.AsyncWaitHandle;

            try
            {
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(_ConnectTimeoutSeconds), false))
                {
                    _TcpClient.Close();
                    throw new TimeoutException("Timeout connecting to " + _ServerIp + ":" + _Port);
                }

                _TcpClient.EndConnect(ar); 
                _NetworkStream = _TcpClient.GetStream();

                if (_Ssl)
                {
                    if (AcceptInvalidCertificates)
                    {
                        // accept invalid certs
                        _SslStream = new SslStream(_NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                    }
                    else
                    {
                        // do not accept invalid SSL certificates
                        _SslStream = new SslStream(_NetworkStream, false);
                    }

                    _SslStream.AuthenticateAsClient(_ServerIp, _SslCertCollection, SslProtocols.Tls12, !AcceptInvalidCertificates);

                    if (!_SslStream.IsEncrypted)
                    {
                        throw new AuthenticationException("Stream is not encrypted");
                    }

                    if (!_SslStream.IsAuthenticated)
                    {
                        throw new AuthenticationException("Stream is not authenticated");
                    }

                    if (MutuallyAuthenticate && !_SslStream.IsMutuallyAuthenticated)
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

            Connected?.Invoke(this, EventArgs.Empty);

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

                if (_TcpClient != null)
                {
                    _TcpClient.Close();
                    _TcpClient.Dispose();
                    _TcpClient = null;
                }

                Logger?.Invoke("[SimpleTcp.Client] Dispose complete");
            }
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        { 
            return AcceptInvalidCertificates;
        }

        private async Task DataReceiver(CancellationToken token)
        {
            try
            { 
                while (true)
                {
                    if (token.IsCancellationRequested
                        || _TcpClient == null 
                        || !_TcpClient.Connected)
                    {
                        Logger?.Invoke("[SimpleTcp.Client] Disconnection detected");
                        break;
                    }
                     
                    byte[] data = await DataReadAsync(token);
                    if (data == null)
                    { 
                        await Task.Delay(30);
                        continue;
                    }

                    DataReceived?.Invoke(this, new DataReceivedFromServerEventArgs(data));
                    _Stats.ReceivedBytes += data.Length;
                } 
            }
            catch (ObjectDisposedException)
            {

            }
            catch (SocketException)
            {
                Logger?.Invoke("[SimpleTcp.Server] Data receiver socket exception (disconnection)");
            }
            catch (Exception e)
            {
                Logger?.Invoke("[SimpleTcp.Client] Data receiver exception:" + 
                    Environment.NewLine + 
                    e.ToString() + 
                    Environment.NewLine);
            }

            _Connected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private async Task<byte[]> DataReadAsync(CancellationToken token)
        { 
            if (_TcpClient == null 
                || !_TcpClient.Connected
                || token.IsCancellationRequested) 
                throw new OperationCanceledException();

            if (!_NetworkStream.CanRead)
                throw new IOException();

            if (_Ssl && !_SslStream.CanRead)
                throw new IOException();
             
            byte[] buffer = new byte[_StreamBufferSize];
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
            byte[] buffer = new byte[_StreamBufferSize];

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
                        _Stats.SentBytes += bytesRead;
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
            byte[] buffer = new byte[_StreamBufferSize];

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
                        _Stats.SentBytes += bytesRead;
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

        #endregion
    }
}
