using System;

namespace SimpleTcp
{
    public class ClientDisconnectedEventArgs : EventArgs
    {
        internal ClientDisconnectedEventArgs(string ipAndPort, DisconnectReason reason)
        {
            IpAndPort = ipAndPort;
            Reason = reason;
        }

        public string IpAndPort { get; }
        public DisconnectReason Reason { get; }
    }
}