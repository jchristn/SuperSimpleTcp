namespace SuperSimpleTcp
{
    using System;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ClientMetadata : IDisposable
    {
        #region Public-Members

        internal TcpClient Client
        {
            get { return _tcpClient; }
        }
         
        internal NetworkStream NetworkStream
        {
            get { return _networkStream; }
        }

        internal SslStream SslStream
        {
            get { return _sslStream; }
            set { _sslStream = value; }
        }

        internal string IpPort
        {
            get { return _ipPort; }
        }

        internal SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);
        internal SemaphoreSlim ReceiveLock = new SemaphoreSlim(1, 1);

        internal CancellationTokenSource TokenSource { get; set; }

        internal CancellationToken Token { get; set; }

        internal Task ReceiveTask { get; set; }

        internal byte[] ProbeBuffer { get; } = new byte[1];

        internal long LastSeenTimestamp
        {
            get { return Interlocked.Read(ref _lastSeenTimestamp); }
        }

        internal DisconnectReason DisconnectReason
        {
            get { return (DisconnectReason)Volatile.Read(ref _disconnectReason); }
        }

        #endregion

        #region Private-Members
         
        private TcpClient _tcpClient = null;
        private NetworkStream _networkStream = null;
        private SslStream _sslStream = null;
        private string _ipPort = null; 
        private long _lastSeenTimestamp = MonotonicTime.GetTimestamp();
        private int _disconnectReason = (int)DisconnectReason.None;
        private int _disposed;

        #endregion

        #region Constructors-and-Factories

        internal ClientMetadata(System.Net.Sockets.TcpClient tcp)
        {
            if (tcp == null) throw new ArgumentNullException(nameof(tcp));

            _tcpClient = tcp;
            _networkStream = tcp.GetStream();
            _ipPort = tcp.Client.RemoteEndPoint.ToString();
            TokenSource = new CancellationTokenSource();
            Token = TokenSource.Token;
        }

        #endregion

        #region Public-Methods

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (TokenSource != null)
            {
                if (!TokenSource.IsCancellationRequested)
                {
                    TokenSource.Cancel();
                    TokenSource.Dispose();
                }
            }

            if (_sslStream != null)
            {
                _sslStream.Close(); 
            }

            if (_networkStream != null)
            {
                _networkStream.Close(); 
            }

            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient.Dispose(); 
            }

            SendLock.Dispose();
            ReceiveLock.Dispose();
        }

        internal void UpdateLastSeen(long timestamp)
        {
            Interlocked.Exchange(ref _lastSeenTimestamp, timestamp);
        }

        internal bool TryMarkDisconnectReason(DisconnectReason reason)
        {
            while (true)
            {
                int current = Volatile.Read(ref _disconnectReason);
                if (current != (int)DisconnectReason.None)
                {
                    return current == (int)reason;
                }

                if (Interlocked.CompareExchange(ref _disconnectReason, (int)reason, current) == current)
                {
                    return true;
                }
            }
        }

        #endregion
    }
}
