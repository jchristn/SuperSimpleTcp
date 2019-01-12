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
    /// TCP client with SSL support.  Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
    /// </summary>
    public class TcpClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Callback to call when the connection is established.
        /// </summary>
        public Func<bool> Connected = null;

        /// <summary>
        /// Callback to call when the connection is destroyed.
        /// </summary>
        public Func<bool> Disconnected = null;

        /// <summary>
        /// Callback to call when byte data has become available from the server.
        /// </summary>
        public Func<byte[], bool> DataReceived = null;

        /// <summary>
        /// Receive buffer size to use while reading from the TCP server.
        /// </summary>
        public int ReceiveBufferSize
        {
            get
            {
                return _ReceiveBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ReceiveBuffer must be one or greater.");
                if (value > 65536) throw new ArgumentException("ReceiveBuffer must be less than 65,536.");
                _ReceiveBufferSize = value;
            }
        }

        /// <summary>
        /// Enable or disable logging to the console.
        /// </summary>
        public bool ConsoleLogging { get; set; }

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
                IsConnected = value;
            }
        }

        #endregion

        #region Private-Members

        private bool _Disposed = false;

        private int _ReceiveBufferSize;

        private string _ServerIp;
        private IPAddress _IPAddress;
        private int _Port;
        private bool _Ssl;
        private string _PfxCertFilename;
        private string _PfxPassword;

        private System.Net.Sockets.TcpClient _TcpClient;

        private SslStream _SslStream;
        private X509Certificate2 _SslCertificate;
        private X509Certificate2Collection _SslCertificateCollection;

        private readonly object _SendLock;

        private string _SourceIp;
        private int _SourcePort;
        private bool _Connected = false;

        private CancellationTokenSource _TokenSource;
        private CancellationToken _Token;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP client.  Set the Connected, Disconnected, and DataReceived callbacks.  Once set, use Connect() to connect to the server.
        /// </summary>
        /// <param name="serverIp">The server IP address.</param>
        /// <param name="port">The TCP port on which to connect.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public TcpClient(string serverIp, int port, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _ReceiveBufferSize = 4096;

            _ServerIp = serverIp;
            _IPAddress = IPAddress.Parse(_ServerIp);
            _Port = port;
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
            _TcpClient = new System.Net.Sockets.TcpClient();

            _SendLock = new object();

            ConsoleLogging = false;

            _SslStream = null;
            _SslCertificate = null;
            _SslCertificateCollection = null;

            if (_Ssl)
            {
                if (String.IsNullOrEmpty(pfxPassword))
                {
                    _SslCertificate = new X509Certificate2(pfxCertFilename);
                }
                else
                {
                    _SslCertificate = new X509Certificate2(pfxCertFilename, pfxPassword);
                }

                _SslCertificateCollection = new X509Certificate2Collection
                {
                    _SslCertificate
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
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), false))
                {
                    _TcpClient.Close();
                    throw new TimeoutException("Timeout connecting to " + _ServerIp + ":" + _Port);
                }

                _TcpClient.EndConnect(ar);

                _SourceIp = ((IPEndPoint)_TcpClient.Client.LocalEndPoint).Address.ToString();
                _SourcePort = ((IPEndPoint)_TcpClient.Client.LocalEndPoint).Port;

                if (_Ssl)
                {
                    if (AcceptInvalidCertificates)
                    {
                        // accept invalid certs
                        _SslStream = new SslStream(_TcpClient.GetStream(), false, new RemoteCertificateValidationCallback(AcceptCertificate));
                    }
                    else
                    {
                        // do not accept invalid SSL certificates
                        _SslStream = new SslStream(_TcpClient.GetStream(), false);
                    }

                    _SslStream.AuthenticateAsClient(_ServerIp, _SslCertificateCollection, SslProtocols.Tls12, !AcceptInvalidCertificates);

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

            if (Connected != null)
            {
                Task.Run(() => Connected());
            }

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;
            Task.Run(async () => await DataReceiver(_Token), _Token);
        }

        /// <summary>
        /// Send data to the server.
        /// </summary> 
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (!_Connected) throw new IOException("Not connected to the server; use Connect() first.");

            lock (_SendLock)
            {
                if (!_Ssl)
                {
                    #region TCP

                    NetworkStream ns = _TcpClient.GetStream();
                    ns.Write(data, 0, data.Length);
                    ns.Flush();

                    #endregion
                }
                else
                {
                    #region SSL

                    _SslStream.Write(data, 0, data.Length);
                    _SslStream.Flush();

                    #endregion
                }
            }
        }

        #endregion

        #region Private-Methods

        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_TcpClient != null)
                {
                    if (_TcpClient.Connected)
                    {
                        NetworkStream ns = _TcpClient.GetStream();
                        if (ns != null)
                        {
                            ns.Close();
                        }
                    }

                    _TcpClient.Close();
                }

                if (_SslStream != null)
                {
                    _SslStream.Dispose();
                }

                _TokenSource.Cancel();
                _TokenSource.Dispose();

                _Connected = false;
            }

            _Disposed = true;
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // return true; // Allow untrusted certificates.
            return AcceptInvalidCertificates;
        }

        private void Log(string msg)
        {
            if (ConsoleLogging) Console.WriteLine(msg);
        }

        private async Task DataReceiver(CancellationToken? cancelToken = null)
        {
            try
            {
                #region Wait-for-Data

                while (true)
                {
                    cancelToken?.ThrowIfCancellationRequested();

                    #region Check-if-Client-Connected-to-Server

                    if (_TcpClient == null)
                    {
                        Log("*** DataReceiver null TCP interface detected, disconnection or close assumed");
                        break;
                    }

                    if (!_TcpClient.Connected)
                    {
                        Log("*** DataReceiver server disconnected");
                        break;
                    }

                    #endregion

                    #region Read-Message-and-Handle

                    byte[] data = await DataReadAsync();
                    if (data == null)
                    {
                        await Task.Delay(30);
                        continue;
                    }

                    if (DataReceived != null)
                    {
                        Task<bool> unawaited = Task.Run(() => DataReceived(data));
                    }

                    #endregion
                }

                #endregion
            }
            catch (OperationCanceledException)
            {
                throw; // normal cancellation
            }
            catch (Exception)
            {
                Log("*** DataReceiver server disconnected");
            }
            finally
            {
                _Connected = false;
                Disconnected?.Invoke();
            }
        }

        private async Task<byte[]> DataReadAsync()
        {
            /*
             *
             * Do not catch exceptions, let them get caught by the data reader
             * to destroy the connection
             *
             */

            try
            {
                if (_TcpClient == null)
                {
                    Log("*** DataReadAsync null client supplied");
                    return null;
                }

                if (!_TcpClient.Connected)
                {
                    Log("*** DataReadAsync supplied client is not connected");
                    return null;
                }

                if (_Ssl && !_SslStream.CanRead)
                {
                    Log("*** DataReadAsync SSL stream is unreadable");
                    return null;
                }

                byte[] buffer = new byte[_ReceiveBufferSize];
                int read = 0;

                if (!_Ssl)
                {
                    #region TCP

                    NetworkStream networkStream = _TcpClient.GetStream();
                    if (!networkStream.CanRead && !networkStream.DataAvailable)
                    {
                        return null;
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        while (true)
                        {
                            read = await networkStream.ReadAsync(buffer, 0, buffer.Length);

                            if (read > 0)
                            {
                                ms.Write(buffer, 0, read);
                                return ms.ToArray();
                            }
                        }
                    }

                    #endregion
                }
                else
                {
                    #region SSL

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
                        }
                    }

                    #endregion
                }
            }
            catch (Exception)
            {
                Log("*** DataReadAsync server disconnected");
                return null;
            }
        }

        #endregion
    }
}
