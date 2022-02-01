using System;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp server events.
    /// </summary>
    public class SimpleTcpServerEvents
    {
        #region Public-Members

        /// <summary>
        /// Event to call when a client connects.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ClientConnected;

        /// <summary>
        /// Event to call when a client disconnects.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ClientDisconnected;

        /// <summary>
        /// Event to call when byte data has become available from the client.
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SimpleTcpServerEvents()
        {

        }

        #endregion

        #region Public-Methods

        internal void HandleClientConnected(object sender, ConnectionEventArgs args)
        {
            ClientConnected?.Invoke(sender, args);
        }

        internal void HandleClientDisconnected(object sender, ConnectionEventArgs args)
        {
            ClientDisconnected?.Invoke(sender, args);
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
