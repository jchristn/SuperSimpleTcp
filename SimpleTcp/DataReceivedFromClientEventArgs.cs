using System;

namespace SimpleTcp
{
    public class DataReceivedFromClientEventArgs : EventArgs
    {
        internal DataReceivedFromClientEventArgs(string ipAndPort, byte[] data)
        {
            IpAndPort = ipAndPort;
            Data = data;
        }

        public string IpAndPort { get; }
        public byte[] Data { get; }
    }
}