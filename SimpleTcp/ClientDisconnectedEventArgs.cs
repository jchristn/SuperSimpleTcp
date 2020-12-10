using System;

namespace SimpleTcp
{
    /// <summary>
    /// Arguments for client disconnection events.
    /// Only the server has visibility to disconnect reasons, as this information is not sent to the client.
    /// To the client, every disconnect appears to be a normal disconnect.
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