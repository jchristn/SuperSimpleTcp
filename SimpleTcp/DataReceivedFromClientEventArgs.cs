using System;

namespace SimpleTcp
{
    /// <summary>
    /// Arguments for data received from client events.
    /// </summary>
    public class DataReceivedFromClientEventArgs : EventArgs
    {
        internal DataReceivedFromClientEventArgs(string ipPort, byte[] data)
        {
            IpPort = ipPort;
            Data = data;
        }

        /// <summary>
        /// The IP address and port number of the connected client socket.
        /// </summary>
        public string IpPort { get; }

        /// <summary>
        /// The data received from the client.
        /// </summary>
        public byte[] Data { get; }
    }
}