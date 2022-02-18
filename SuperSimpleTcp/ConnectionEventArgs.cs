using System;

namespace SuperSimpleTcp
{
    /// <summary>
    /// Arguments for connection events.
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        internal ConnectionEventArgs(string ipPort, DisconnectReason reason = DisconnectReason.None)
        {
            IpPort = ipPort;
            Reason = reason;
        }

        /// <summary>
        /// The IP address and port number of the connected peer socket.
        /// </summary>
        public string IpPort { get; }

        /// <summary>
        /// The reason for the disconnection, if any.
        /// </summary>
        public DisconnectReason Reason { get; } = DisconnectReason.None;
    }
}