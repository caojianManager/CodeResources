using System;

namespace EEGTool.CommunicationStrategy
{
    public sealed class CommunicationDataReceivedEventArgs : EventArgs
    {
        public CommunicationDataReceivedEventArgs(byte[] data)
        {
            Data = data ?? Array.Empty<byte>();
            Timestamp = DateTimeOffset.Now;
        }

        public byte[] Data { get; }
        public DateTimeOffset Timestamp { get; }
    }
}
