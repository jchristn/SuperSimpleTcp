using System;

namespace SimpleTcp
{
    public class ClientConnectedEventArgs : EventArgs
    {
        internal ClientConnectedEventArgs(string ipAndPort)
        {
            IpAndPort = ipAndPort;
        }

        public string IpAndPort { get; }
    }
}