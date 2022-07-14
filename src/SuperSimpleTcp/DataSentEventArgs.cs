using System;

namespace SuperSimpleTcp
{
    /// <summary>
    /// Arguments for data sent to a connected endpoint.
    /// </summary>
    public class DataSentEventArgs : EventArgs
    {
        internal DataSentEventArgs(string ipPort, long bytesSent)
        {
            IpPort = ipPort;
            BytesSent = bytesSent;
        }

        /// <summary>
        /// The IP address and port number of the connected endpoint.
        /// </summary>
        public string IpPort { get; }

        /// <summary>
        /// The number of bytes sent.
        /// </summary>
        public long BytesSent { get; }
    }
}