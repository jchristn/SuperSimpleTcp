using System;

namespace SimpleTcp
{
    /// <summary>
    /// Arguments for client disconnection events.
    /// </summary>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        internal ClientDisconnectedEventArgs(string ipPort, DisconnectReason reason)
        {
            IpPort = ipPort;
            Reason = reason;
        }

        /// <summary>
        /// The IP address and port number of the disconnected client socket.
        /// </summary>
        public string IpPort { get; }

        /// <summary>
        /// The reason for the disconnection.
        /// </summary>
        public DisconnectReason Reason { get; }
    }
}