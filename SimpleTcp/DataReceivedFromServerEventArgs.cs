using System;

namespace SimpleTcp
{
    /// <summary>
    /// Arguments for data received from server events.
    /// </summary>
    public class DataReceivedFromServerEventArgs : EventArgs
    {
        internal DataReceivedFromServerEventArgs(byte[] data)
        {
            Data = data;
        }

        /// <summary>
        /// The data received from the server.
        /// </summary>
        public byte[] Data { get; }
    }
}