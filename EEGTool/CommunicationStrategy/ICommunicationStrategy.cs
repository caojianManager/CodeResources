using System;
using System.Threading;
using System.Threading.Tasks;

namespace EEGTool.CommunicationStrategy
{
    public interface ICommunicationStrategy : IDisposable
    {
        CommunicationType Type { get; }

        bool IsConnected { get; }

        event EventHandler<CommunicationStateChangedEventArgs>? ConnectionStateChanged;
        event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred;

        Task ConnectAsync(CommunicationOptions options, CancellationToken cancellationToken = default);

        Task DisconnectAsync();

        Task SendAsync(byte[] data, CancellationToken cancellationToken = default);
    }
}
