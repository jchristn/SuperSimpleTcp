using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleTcp
{
    internal class ClientMetadata : IDisposable
    {
        #region Public-Members

        internal System.Net.Sockets.TcpClient Client
        {
            get { return _TcpClient; }
        }
         
        internal NetworkStream NetworkStream
        {
            get { return _NetworkStream; }
        }

        internal SslStream SslStream
        {
            get { return _SslStream; }
            set { _SslStream = value; }
        }

        internal string IpPort
        {
            get { return _IpPort; }
        }

        internal object SendLock
        {
            get { return _Lock; }
        }

        internal CancellationTokenSource TokenSource { get; set; }

        internal CancellationToken Token { get; set; }

        #endregion

        #region Private-Members
         
        private System.Net.Sockets.TcpClient _TcpClient = null;
        private NetworkStream _NetworkStream = null;
        private SslStream _SslStream = null;
        private string _IpPort = null;
        private readonly object _Lock = new object();

        #endregion

        #region Constructors-and-Factories

        internal ClientMetadata(System.Net.Sockets.TcpClient tcp)
        {
            if (tcp == null) throw new ArgumentNullException(nameof(tcp));

            _TcpClient = tcp;
            _NetworkStream = tcp.GetStream();
            _IpPort = tcp.Client.RemoteEndPoint.ToString();
            TokenSource = new CancellationTokenSource();
            Token = TokenSource.Token;
        }

        #endregion

        #region Public-Methods

        public void Dispose()
        {
            lock (_Lock)
            {
                if (TokenSource != null)
                {
                    if (!TokenSource.IsCancellationRequested) TokenSource.Cancel();
                    TokenSource.Dispose();
                    TokenSource = null;
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
                    // _NetworkStream.Dispose();
                    // _NetworkStream = null;
                }

                if (_TcpClient != null)
                {
                    _TcpClient.Close();
                    _TcpClient.Dispose();
                    _TcpClient = null;
                }
            }
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
