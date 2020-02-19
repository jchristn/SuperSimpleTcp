using System;

namespace SimpleTcp
{
    public class DataReceivedFromServerEventArgs : EventArgs
    {
        internal DataReceivedFromServerEventArgs(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; }
    }
}