using System;

namespace EEGTool.CommunicationStrategy
{
    public sealed class CommunicationErrorEventArgs : EventArgs
    {
        public CommunicationErrorEventArgs(string message, Exception? exception = null)
        {
            Message = message ?? string.Empty;
            Exception = exception;
            Timestamp = DateTimeOffset.Now;
        }

        public string Message { get; }
        public Exception? Exception { get; }
        public DateTimeOffset Timestamp { get; }
    }
}
