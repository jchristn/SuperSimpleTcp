using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp server events.
    /// </summary>
    public class SimpleTcpServerEvents
    {
        #region Public-Members

        /// <summary>
        /// Event to call when a client connects.  A string containing the client IP:port will be passed.
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// Event to call when a client disconnects.  A string containing the client IP:port will be passed.
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>
        /// Event to call when byte data has become available from the client.  A string containing the client IP:port and a byte array containing the data will be passed.
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

        internal void HandleClientConnected(object sender, ClientConnectedEventArgs args)
        {
            ClientConnected?.Invoke(sender, args);
        }

        internal void HandleClientDisconnected(object sender, ClientDisconnectedEventArgs args)
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
