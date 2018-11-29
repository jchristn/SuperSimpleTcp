using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace SimpleTcp
{
    internal class ClientMetadata : IDisposable
    {
        #region Public-Members

        internal System.Net.Sockets.TcpClient TcpClient
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

        internal object Lock
        {
            get { return _Lock; }
        }

        #endregion

        #region Private-Members

        private bool _Disposed = false;

        private System.Net.Sockets.TcpClient _TcpClient = null;
        private NetworkStream _NetworkStream = null;
        private SslStream _SslStream = null;
        private string _IpPort = null;
        private readonly object _Lock = new object();

        #endregion

        #region Constructors-and-Factories

        internal ClientMetadata(System.Net.Sockets.TcpClient tcp)
        {
            _TcpClient = tcp ?? throw new ArgumentNullException(nameof(tcp));

            _NetworkStream = tcp.GetStream();

            _IpPort = tcp.Client.RemoteEndPoint.ToString();
        }

        #endregion

        #region Public-Methods

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                if (_SslStream != null)
                {
                    _SslStream.Close();
                }

                if (_NetworkStream != null)
                {
                    _NetworkStream.Close();
                }

                if (_TcpClient != null)
                {
                    _TcpClient.Close();
                }
            }

            _Disposed = true;
        }

        #endregion
    }
}
