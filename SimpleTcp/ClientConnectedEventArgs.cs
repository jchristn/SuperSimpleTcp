using System;

namespace SimpleTcp
{
    /// <summary>
    /// Arguments for client connection events.
    /// </summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        internal ClientConnectedEventArgs(string ipPort)
        {
            IpPort = ipPort;
        }

        /// <summary>
        /// The IP address and port number of the connected client socket.
        /// </summary>
        public string IpPort { get; }
    }
}