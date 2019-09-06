using System;
using System.Collections.Concurrent;
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
    /// TCP server with SSL support.  
    /// Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  
    /// Once set, use Start() to begin listening for connections.
    /// </summary>
    public class TcpServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Callback to call when a client connects.  A string containing the client IP:port will be passed.
        /// </summary>
        public Func<string, Task> ClientConnected = null;

        /// <summary>
        /// Callback to call when a client disconnects.  A string containing the client IP:port will be passed.
        /// </summary>
        public Func<string, Task> ClientDisconnected = null;

        /// <summary>
        /// Callback to call when byte data has become available from the client.  A string containing the client IP:port and a byte array containing the data will be passed.
        /// </summary>
        public Func<string, byte[], Task> DataReceived = null;

        /// <summary>
        /// Receive buffer size to use while reading from connected TCP clients.
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

        #endregion

        #region Private-Members

        private bool _Disposed = false;

        private int _ReceiveBufferSize;

        private string _ListenerIp;
        private IPAddress _IPAddress;
        private int _Port;
        private bool _Ssl;
        private string _PfxCertFilename;
        private string _PfxPassword;

        private X509Certificate2 _SslCertificate;
        private X509Certificate2Collection _SslCertificateCollection;

        private ConcurrentDictionary<string, ClientMetadata> _Clients;

        private TcpListener _Listener;
        private bool _Running;

        private CancellationTokenSource _TokenSource;
        private CancellationToken _Token;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP server.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="listenerIp">The listener IP address.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public TcpServer(string listenerIp, int port, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (String.IsNullOrEmpty(listenerIp)) throw new ArgumentNullException(nameof(listenerIp));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _ReceiveBufferSize = 4096;

            _ListenerIp = listenerIp;
            _IPAddress = IPAddress.Parse(_ListenerIp);
            _Port = port;
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
            _Running = false;
            _Clients = new ConcurrentDictionary<string, ClientMetadata>();

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;

            ConsoleLogging = false;

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
        /// Dispose of the TCP server.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start the TCP server and begin accepting connections.
        /// </summary>
        public void Start()
        {
            if (_Running) throw new InvalidOperationException("TcpServer is already running.");

            _Listener = new TcpListener(_IPAddress, _Port);
            _Listener.Start();

            _Clients = new ConcurrentDictionary<string, ClientMetadata>();

            Task.Run(() => AcceptConnections(), _Token);
        }

        /// <summary>
        /// Retrieve a list of client IP:port connected to the server.
        /// </summary>
        /// <returns>List of strings, each containing client IP:port.</returns>
        public List<string> GetClients()
        {
            List<string> clients = new List<string>(_Clients.Keys);
            return clients;
        }

        /// <summary>
        /// Determines if a client is connected by its IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <returns>True if connected.</returns>
        public bool IsConnected(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ClientMetadata client = null;
            return (_Clients.TryGetValue(ipPort, out client));
        }

        /// <summary>
        /// Asynchronously send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(string ipPort, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            ClientMetadata client = GetClient(ipPort);

            lock (client.Lock)
            {
                if (!_Ssl)
                {
                    client.NetworkStream.Write(data, 0, data.Length);
                    client.NetworkStream.Flush();
                }
                else
                {
                    client.SslStream.Write(data, 0, data.Length);
                    client.SslStream.Flush();
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
                _TokenSource.Cancel();
                _TokenSource.Dispose();

                if (_Listener != null && _Listener.Server != null)
                {
                    _Listener.Server.Close();
                    _Listener.Server.Dispose();
                }

                if (_Clients != null && _Clients.Count > 0)
                {
                    foreach (KeyValuePair<string, ClientMetadata> currMetadata in _Clients)
                    {
                        currMetadata.Value.Dispose();
                    }
                }
            }

            _Disposed = true;
        }

        private void Log(string msg)
        {
            if (ConsoleLogging) Console.WriteLine(msg);
        }

        private async void AcceptConnections()
        {
            while (!_Token.IsCancellationRequested)
            {
                try
                {
                    System.Net.Sockets.TcpClient tcpClient = await _Listener.AcceptTcpClientAsync();
                    tcpClient.LingerState.Enabled = false;
                    string clientIp = tcpClient.Client.RemoteEndPoint.ToString();

                    ClientMetadata clientMetadata = new ClientMetadata(tcpClient);

                    if (_Ssl)
                    {
                        if (AcceptInvalidCertificates)
                        {
                            // accept invalid certs
                            clientMetadata.SslStream = new SslStream(clientMetadata.NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                        }
                        else
                        {
                            // do not accept invalid SSL certificates
                            clientMetadata.SslStream = new SslStream(clientMetadata.NetworkStream, false);
                        }

                        Task unawaited = Task.Run(() =>
                        {
                            Task<bool> success = StartTls(clientMetadata);
                        }, _Token);
                    }

                    AddClient(clientIp, clientMetadata);
                    Log("*** FinalizeConnection Starting data receiver [" + clientIp + "]");
                    if (ClientConnected != null) await Task.Run(() => ClientConnected(clientIp));
                    Task unawaited2 = Task.Run(() => DataReceiver(clientMetadata), _Token);
                }
                catch (Exception ex)
                {
                    Log("*** AcceptConnections exception: " + ex.Message);
                }
            }
        }

        private async Task<bool> StartTls(ClientMetadata client)
        {
            try
            {
                // the two bools in this should really be contruction paramaters
                // maybe re-use mutualAuthentication and acceptInvalidCerts ?
                await client.SslStream.AuthenticateAsServerAsync(_SslCertificate, true, SslProtocols.Tls12, false);

                if (!client.SslStream.IsEncrypted)
                {
                    Log("*** StartTls stream from " + client.IpPort + " not encrypted");
                    client.Dispose();
                    return false;
                }

                if (!client.SslStream.IsAuthenticated)
                {
                    Log("*** StartTls stream from " + client.IpPort + " not authenticated");
                    client.Dispose();
                    return false;
                }

                if (MutuallyAuthenticate && !client.SslStream.IsMutuallyAuthenticated)
                {
                    Log("*** StartTls stream from " + client.IpPort + " failed mutual authentication");
                    client.Dispose();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log("*** StartTls Exception from " + client.IpPort + Environment.NewLine + ex.ToString());
                client.Dispose();
                return false;
            }

            return true;
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // return true; // Allow untrusted certificates.
            return AcceptInvalidCertificates;
        }

        private bool IsClientConnected(ClientMetadata client)
        {
            if (client.TcpClient.Connected)
            {
                if ((client.TcpClient.Client.Poll(0, SelectMode.SelectWrite)) && (!client.TcpClient.Client.Poll(0, SelectMode.SelectError)))
                {
                    byte[] buffer = new byte[1];
                    if (client.TcpClient.Client.Receive(buffer, SocketFlags.Peek) == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private async Task DataReceiver(ClientMetadata client)
        {
            try
            {
                #region Wait-for-Data

                while (true)
                {
                    try
                    {
                        if (!IsClientConnected(client))
                        {
                            break;
                        }

                        byte[] data = await DataReadAsync(client);
                        if (data == null)
                        {
                            // no message available 
                            await Task.Delay(30);
                            continue;
                        }

                        if (DataReceived != null)
                        {
                            Task unawaited = Task.Run(() => DataReceived(client.IpPort, data));
                        }
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }

                #endregion
            }
            finally
            {
                RemoveClient(client.IpPort);
                Log("*** [" + client.IpPort + "] DataReceiver disconnect detected");
                if (ClientDisconnected != null) await Task.Run(() => ClientDisconnected(client.IpPort));
                client.Dispose();
            }
        }

        private void AddClient(string ipPort, ClientMetadata client)
        {
            _Clients.TryAdd(ipPort, client);
        }

        private void RemoveClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ClientMetadata client;
            _Clients.TryRemove(ipPort, out client);
        }

        private ClientMetadata GetClient(string ipPort)
        {
            ClientMetadata client;
            if (!_Clients.TryGetValue(ipPort, out client))
            {
                throw new KeyNotFoundException("Client IP " + ipPort + " not found.");
            }

            return client;
        }

        private async Task<byte[]> DataReadAsync(ClientMetadata client)
        {
            /*
             *
             * Do not catch exceptions, let them get caught by the data reader
             * to destroy the connection
             *
             */

            try
            {
                if (!client.NetworkStream.CanRead) return null;
                if (!client.NetworkStream.DataAvailable) return null;
                if (_Ssl && !client.SslStream.CanRead) return null;

                byte[] buffer = new byte[_ReceiveBufferSize];
                int read = 0;

                if (!_Ssl)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        while (true)
                        {
                            read = await client.NetworkStream.ReadAsync(buffer, 0, buffer.Length);

                            if (read > 0)
                            {
                                ms.Write(buffer, 0, read);
                                return ms.ToArray();
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
                            read = await client.SslStream.ReadAsync(buffer, 0, buffer.Length);

                            if (read > 0)
                            {
                                ms.Write(buffer, 0, read);
                                return ms.ToArray();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                Log("*** [" + client.IpPort + "] DataReadAsync server disconnected");
                return null;
            }
        }

        #endregion
    }
}
