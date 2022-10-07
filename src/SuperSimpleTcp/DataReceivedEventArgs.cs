using System;

namespace SuperSimpleTcp
{
    /// <summary>
    /// Arguments for data received from connected endpoints.
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        internal DataReceivedEventArgs(string ipPort, ArraySegment<byte> data)
        {
            IpPort = ipPort;
            Data = data;
        }

        /// <summary>
        /// The IP address and port number of the connected endpoint.
        /// </summary>
        public string IpPort { get; }

        /// <summary>
        /// The data received from the endpoint.
        /// </summary>
        public ArraySegment<byte> Data { get; }
    }
}