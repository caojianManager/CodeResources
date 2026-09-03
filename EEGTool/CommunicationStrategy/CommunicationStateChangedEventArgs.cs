using System;

namespace EEGTool.CommunicationStrategy
{
    public sealed class CommunicationStateChangedEventArgs : EventArgs
    {
        public CommunicationStateChangedEventArgs(bool isConnected, string reason)
        {
            IsConnected = isConnected;
            Reason = reason ?? string.Empty;
            Timestamp = DateTimeOffset.Now;
        }

        public bool IsConnected { get; }
        public string Reason { get; }
        public DateTimeOffset Timestamp { get; }
    }
}
