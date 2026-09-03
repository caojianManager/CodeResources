using BrainZoneMultichannel.FrameWork.NRF52840;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EEGTool.CommunicationStrategy
{
    public sealed class CdcCommunicationStrategy : ICommunicationStrategy
    {
        private readonly CDCConnector _connector;
        private bool _disposed;

        public CdcCommunicationStrategy()
            : this(new CDCConnector())
        {
        }

        public CdcCommunicationStrategy(CDCConnector connector)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _connector.Connected += OnConnected;
            _connector.Disconnected += OnDisconnected;
            _connector.BytesReceived += OnBytesReceived;
            _connector.ErrorOccurred += OnErrorOccurred;
        }

        public CommunicationType Type => CommunicationType.Cdc;

        public bool IsConnected => _connector.IsOpen;

        public event EventHandler<CommunicationStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred;

        public async Task ConnectAsync(CommunicationOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (options is not CdcCommunicationOptions cdcOptions)
            {
                throw new ArgumentException("CDC 通信策略需要 CdcCommunicationOptions", nameof(options));
            }

            cancellationToken.ThrowIfCancellationRequested();
            bool isOpened = await _connector.OpenAsync(cdcOptions.Options);
            if (!isOpened)
            {
                throw new InvalidOperationException($"打开串口 {cdcOptions.Options.PortName} 失败");
            }
        }

        public Task DisconnectAsync()
        {
            ThrowIfDisposed();
            return _connector.CloseAsync();
        }

        public Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _connector.SendBytesAsync(data, cancellationToken);
        }

        private void OnConnected()
        {
            ConnectionStateChanged?.Invoke(
                this,
                new CommunicationStateChangedEventArgs(true, "Connected"));
        }

        private void OnDisconnected()
        {
            ConnectionStateChanged?.Invoke(
                this,
                new CommunicationStateChangedEventArgs(false, "Disconnected"));
        }

        private void OnBytesReceived(byte[] data)
        {
            DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(data));
        }

        private void OnErrorOccurred(CDCErrorCode code, string message)
        {
            ErrorOccurred?.Invoke(
                this,
                new CommunicationErrorEventArgs($"CDC({code}): {message}"));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connector.Connected -= OnConnected;
            _connector.Disconnected -= OnDisconnected;
            _connector.BytesReceived -= OnBytesReceived;
            _connector.ErrorOccurred -= OnErrorOccurred;
            _connector.Dispose();
        }
    }
}
