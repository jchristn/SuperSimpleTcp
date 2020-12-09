using System;
using System.Collections.Generic;
using System.Text;

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
        public event EventHandler Connected;

        /// <summary>
        /// Event to call when the connection is destroyed.
        /// </summary>
        public event EventHandler Disconnected;

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

        internal void HandleConnected(object sender)
        {
            Connected?.Invoke(sender, EventArgs.Empty);
        }

        internal void HandleDisconnected(object sender)
        {
            Disconnected?.Invoke(sender, EventArgs.Empty);
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
