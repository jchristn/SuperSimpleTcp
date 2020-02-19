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
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// Callback to call when a client disconnects.  A string containing the client IP:port will be passed.
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>
        /// Callback to call when byte data has become available from the client.  A string containing the client IP:port and a byte array containing the data will be passed.
        /// </summary>
        public event EventHandler<DataReceivedFromClientEventArgs> DataReceived;

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
        /// Maximum amount of time to wait before considering a client idle and disconnecting them. 
        /// By default, this value is set to 0, which will never disconnect a client due to inactivity.
        /// The timeout is reset any time a message is received from a client or a message is sent to a client.
        /// For instance, if you set this value to 30, the client will be disconnected if the server has not received a message from the client within 30 seconds or if a message has not been sent to the client in 30 seconds.
        /// </summary>
        public int IdleClientTimeoutSeconds
        {
            get
            {
                return _IdleClientTimeoutSeconds;
            }
            set
            {
                if (value < 0) throw new ArgumentException("IdleClientTimeoutSeconds must be zero or greater.");
                _IdleClientTimeoutSeconds = value;
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

        private int _ReceiveBufferSize = 4096;
        private int _IdleClientTimeoutSeconds = 0;

        private string _ListenerIp;
        private IPAddress _IPAddress;
        private int _Port;
        private bool _Ssl;
        private string _PfxCertFilename;
        private string _PfxPassword;

        private X509Certificate2 _SslCertificate = null;
        private X509Certificate2Collection _SslCertificateCollection = null;

        private ConcurrentDictionary<string, ClientMetadata> _Clients = new ConcurrentDictionary<string, ClientMetadata>();
        private ConcurrentDictionary<string, DateTime> _ClientsLastSeen = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, DateTime> _ClientsKicked = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, DateTime> _ClientsTimedout = new ConcurrentDictionary<string, DateTime>();

        private TcpListener _Listener;
        private bool _Running;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;

        private Statistics _Stats = new Statistics();

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
             
            _ListenerIp = listenerIp;
            _IPAddress = IPAddress.Parse(_ListenerIp);
            _Port = port;
            _Ssl = ssl;
            _PfxCertFilename = pfxCertFilename;
            _PfxPassword = pfxPassword;
            _Running = false;  
            _Token = _TokenSource.Token;
             
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

            Task.Run(() => MonitorForIdleClients(), _Token);
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
        /// <returns>IEnumerable of strings, each containing client IP:port.</returns>
        public IEnumerable<string> GetClients()
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
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(string ipPort, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            ClientMetadata client = null;
            if (!_Clients.TryGetValue(ipPort, out client)) return;
            if (client == null) return;

            lock (client.SendLock)
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

            _Stats.SentBytes += data.Length;
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">String containing data to send.</param>
        public void Send(string ipPort, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            Send(ipPort, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Disconnects the specified client.
        /// </summary>
        /// <param name="ipPort">IP:port of the client.</param>
        public void DisconnectClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            if (!_Clients.TryGetValue(ipPort, out ClientMetadata client))
            {
                Logger?.Invoke("[SimpleTcp.Server] Unable to find client: " + ipPort); 
            }
            else
            {
                if (!_ClientsTimedout.ContainsKey(ipPort))
                {
                    Logger?.Invoke("[SimpleTcp.Server] Kicking: " + ipPort); 
                    _ClientsKicked.TryAdd(ipPort, DateTime.Now);
                }

                _Clients.TryRemove(client.IpPort, out ClientMetadata destroyed);
                client.Dispose(); 
                Logger?.Invoke("[SimpleTcp.Server] Disposed: " + ipPort); 
            }
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
                    if (_Clients != null && _Clients.Count > 0)
                    {
                        foreach (KeyValuePair<string, ClientMetadata> curr in _Clients)
                        {
                            curr.Value.Dispose();
                            Logger?.Invoke("[SimpleTcp.Server] Disconnected client: " + curr.Key);
                        }
                    }

                    _TokenSource.Cancel();
                    _TokenSource.Dispose();

                    if (_Listener != null && _Listener.Server != null)
                    {
                        _Listener.Server.Close();
                        _Listener.Server.Dispose();
                    }

                    if (_Listener != null)
                    {
                        _Listener.Stop();
                    }
                }
                catch (Exception e)
                {
                    Logger?.Invoke("[SimpleTcp.Server] Dispose exception:" +
                        Environment.NewLine +
                        e.ToString() +
                        Environment.NewLine);
                }
            }
        }
         
        private bool IsClientConnected(System.Net.Sockets.TcpClient client)
        {
            if (client.Connected)
            {
                if ((client.Client.Poll(0, SelectMode.SelectWrite)) && (!client.Client.Poll(0, SelectMode.SelectError)))
                {
                    byte[] buffer = new byte[1];
                    if (client.Client.Receive(buffer, SocketFlags.Peek) == 0)
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

        private async void AcceptConnections()
        {
            while (!_Token.IsCancellationRequested)
            {
                ClientMetadata client = null;

                try
                {
                    System.Net.Sockets.TcpClient tcpClient = await _Listener.AcceptTcpClientAsync(); 
                    string clientIp = tcpClient.Client.RemoteEndPoint.ToString();

                    client = new ClientMetadata(tcpClient);

                    if (_Ssl)
                    {
                        if (AcceptInvalidCertificates)
                        { 
                            client.SslStream = new SslStream(client.NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate));
                        }
                        else
                        { 
                            client.SslStream = new SslStream(client.NetworkStream, false);
                        }

                        bool success = await StartTls(client);
                        if (!success)
                        {
                            client.Dispose();
                            continue;
                        }
                    }

                    _Clients.TryAdd(clientIp, client); 
                    _ClientsLastSeen.TryAdd(clientIp, DateTime.Now); 
                    Logger?.Invoke("[SimpleTcp.Server] Starting data receiver for: " + clientIp); 
                    ClientConnected?.Invoke(this, new ClientConnectedEventArgs(clientIp)); 
                    Task unawaited = Task.Run(() => DataReceiver(client), _Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    if (client != null) client.Dispose();
                    continue;
                }
                catch (Exception e)
                {
                    if (client != null) client.Dispose();
                    Logger?.Invoke("[SimpleTcp.Server] Exception while awaiting connections: " + e.ToString());
                    continue;
                } 
            }
        }

        private async Task<bool> StartTls(ClientMetadata client)
        {
            try
            { 
                await client.SslStream.AuthenticateAsServerAsync(
                    _SslCertificate, 
                    MutuallyAuthenticate, 
                    SslProtocols.Tls12, 
                    !AcceptInvalidCertificates);

                if (!client.SslStream.IsEncrypted)
                {
                    Logger?.Invoke("[SimpleTcp.Server] Client " + client.IpPort + " not encrypted, disconnecting");
                    client.Dispose();
                    return false;
                }

                if (!client.SslStream.IsAuthenticated)
                {
                    Logger?.Invoke("[SimpleTcp.Server] Client " + client.IpPort + " not SSL/TLS authenticated, disconnecting");
                    client.Dispose();
                    return false;
                }

                if (MutuallyAuthenticate && !client.SslStream.IsMutuallyAuthenticated)
                {
                    Logger?.Invoke("[SimpleTcp.Server] Client " + client.IpPort + " failed mutual authentication, disconnecting");
                    client.Dispose();
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger?.Invoke("[SimpleTcp.Server] Client " + client.IpPort + " SSL/TLS exception: " + Environment.NewLine + e.ToString());
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

        private async Task DataReceiver(ClientMetadata client)
        {
            Logger?.Invoke("[SimpleTcp.Server] Data receiver started for client " + client.IpPort);
            
            while (true)
            {
                try
                { 
                    if (client.Token.IsCancellationRequested 
                        || !IsClientConnected(client.Client))
                    {
                        Logger?.Invoke("[SimpleTcp.Server] Client " + client.IpPort + " disconnected");
                        break;
                    }

                    if (client.Token.IsCancellationRequested)
                    {
                        Logger?.Invoke("[SimpleTcp.Server] Cancellation requested (data receiver for client " + client.IpPort + ")");
                        break;
                    } 

                    byte[] data = await DataReadAsync(client);
                    if (data == null)
                    { 
                        await Task.Delay(30);
                        continue;
                    }

                    DataReceived?.Invoke(this, new DataReceivedFromClientEventArgs(client.IpPort, data));
                    _Stats.ReceivedBytes += data.Length;
                    UpdateClientLastSeen(client.IpPort);
                }
                catch (SocketException)
                {
                    Logger?.Invoke("[SimpleTcp.Server] Data receiver socket exception (disconnection) for " + client.IpPort);
                }
                catch (Exception e)
                {
                    Logger?.Invoke("[SimpleTcp.Server] Data receiver exception for client " + client.IpPort + ":" +
                        Environment.NewLine +
                        e.ToString() +
                        Environment.NewLine);

                    break;
                }
            }

            Logger?.Invoke("[SimpleTcp.Server] Data receiver terminated for client " + client.IpPort);

            if (_ClientsKicked.ContainsKey(client.IpPort))
            {
                ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(client.IpPort, DisconnectReason.Kicked));
            }
            else if (_ClientsTimedout.ContainsKey(client.IpPort))
            {
                ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(client.IpPort, DisconnectReason.Timeout));
            }
            else
            {
                ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(client.IpPort, DisconnectReason.Normal));
            }

            DateTime removedTs;
            _Clients.TryRemove(client.IpPort, out ClientMetadata destroyed);
            _ClientsLastSeen.TryRemove(client.IpPort, out removedTs);
            _ClientsKicked.TryRemove(client.IpPort, out removedTs);
            _ClientsTimedout.TryRemove(client.IpPort, out removedTs); 
            client.Dispose();
        }
           
        private async Task<byte[]> DataReadAsync(ClientMetadata client)
        { 
            if (client.Token.IsCancellationRequested) throw new OperationCanceledException();
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
                        read = await client.SslStream.ReadAsync(buffer, 0, buffer.Length);

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

        private async Task MonitorForIdleClients()
        {
            while (!_Token.IsCancellationRequested)
            {
                if (_IdleClientTimeoutSeconds > 0 && _ClientsLastSeen.Count > 0)
                {
                    MonitorForIdleClientsTask();
                }
                await Task.Delay(5000, _Token);
            }
        }

        private void MonitorForIdleClientsTask()
        {
            DateTime idleTimestamp = DateTime.Now.AddSeconds(-1 * _IdleClientTimeoutSeconds);

            foreach (KeyValuePair<string, DateTime> curr in _ClientsLastSeen)
            {
                if (curr.Value < idleTimestamp)
                {
                    _ClientsTimedout.TryAdd(curr.Key, DateTime.Now);
                    Logger?.Invoke("[SimpleTcp.Server] Disconnecting " + curr.Key + " due to timeout");
                    DisconnectClient(curr.Key);
                }
            }
        }

        private void UpdateClientLastSeen(string ipPort)
        {
            if (_ClientsLastSeen.ContainsKey(ipPort))
            {
                DateTime ts;
                _ClientsLastSeen.TryRemove(ipPort, out ts);
            }

            _ClientsLastSeen.TryAdd(ipPort, DateTime.Now);
        }

        #endregion
    }
}
