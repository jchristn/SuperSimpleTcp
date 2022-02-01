using System;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp client events.
    /// </summary>
    public class SimpleTcpClientEvents
    {
        #region Public-Members

        /// <summary>
        /// Event to call when the connection is established.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> Connected;

        /// <summary>
        /// Event to call when the connection is destroyed.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> Disconnected;

        /// <summary>
        /// Event to call when byte data has become available from the server.
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SimpleTcpClientEvents()
        {

        }

        #endregion

        #region Public-Methods

        internal void HandleConnected(object sender, ConnectionEventArgs args)
        {
            Connected?.Invoke(sender, args);
        }

        internal void HandleClientDisconnected(object sender, ConnectionEventArgs args)
        {
            Disconnected?.Invoke(sender, args);
        }

        internal void HandleDataReceived(object sender, DataReceivedEventArgs args)
        {
            DataReceived?.Invoke(sender, args);
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
