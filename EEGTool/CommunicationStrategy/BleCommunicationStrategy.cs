using BLETool;
using EEGTool.Models.BLE;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EEGTool.CommunicationStrategy
{
    public sealed class BleCommunicationStrategy : ICommunicationStrategy
    {
        private readonly BleManager _ble;
        private BleGattCharacteristicInfo? _writeCharacteristic;
        private BleGattCharacteristicInfo? _notifyCharacteristic;
        private bool _isNotifySubscribed;
        private bool _disposed;

        public BleCommunicationStrategy()
            : this(BleToolKit.Shared)
        {
        }

        public BleCommunicationStrategy(BleManager ble)
        {
            _ble = ble ?? throw new ArgumentNullException(nameof(ble));
            _ble.ConnectionChanged += OnBleConnectionChanged;
            _ble.DataReceived += OnBleDataReceived;
        }

        public CommunicationType Type => CommunicationType.Ble;

        public bool IsConnected => _ble.IsConnected;

        public event EventHandler<CommunicationStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationErrorEventArgs>? ErrorOccurred;

        public async Task ConnectAsync(CommunicationOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (options is not BleCommunicationOptions bleOptions)
            {
                throw new ArgumentException("BLE 通信策略需要 BleCommunicationOptions", nameof(options));
            }

            bleOptions.Validate();

            try
            {
                _ble.ConfigureReconnect(
                    bleOptions.EnableReconnect,
                    bleOptions.ReconnectDelayMs,
                    bleOptions.ReconnectMaxAttempts);

                await _ble.ConnectAsync(bleOptions.DeviceId, cancellationToken);
                await RefreshDataChannelAsync(bleOptions.ContinueWhenNotifyAccessDenied);
            }
            catch (Exception ex)
            {
                RaiseError($"蓝牙连接失败: {ex.Message}", ex);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            ThrowIfDisposed();

            try
            {
                if (_notifyCharacteristic != null && _isNotifySubscribed)
                {
                    await _ble.UnsubscribeAsync(
                        _notifyCharacteristic.ServiceUuid,
                        _notifyCharacteristic.Uuid);
                }
            }
            finally
            {
                _writeCharacteristic = null;
                _notifyCharacteristic = null;
                _isNotifySubscribed = false;
                await _ble.DisconnectAsync();
            }
        }

        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (data == null || data.Length == 0)
            {
                return;
            }

            if (_writeCharacteristic == null)
            {
                await RefreshDataChannelAsync(continueWhenNotifyAccessDenied: true);
            }

            if (_writeCharacteristic == null)
            {
                throw new InvalidOperationException("没有找到可用的蓝牙写入通道");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _ble.WriteAsync(
                    _writeCharacteristic.ServiceUuid,
                    _writeCharacteristic.Uuid,
                    data);
            }
            catch (Exception ex)
            {
                RaiseError($"蓝牙发送失败: {ex.Message}", ex);
                throw;
            }
        }

        private async Task RefreshDataChannelAsync(bool continueWhenNotifyAccessDenied)
        {
            var dataChannel = await BleGattProfileHelper.GetDataChannelAsync(
                _ble,
                continueWhenNotifyAccessDenied);

            if (dataChannel == null)
            {
                _writeCharacteristic = null;
                _notifyCharacteristic = null;
                _isNotifySubscribed = false;
                return;
            }

            _writeCharacteristic = dataChannel.WriteCharacteristic;
            _notifyCharacteristic = dataChannel.NotifyCharacteristic;
            _isNotifySubscribed = dataChannel.IsNotifySubscribed;
        }

        private void OnBleConnectionChanged(object? sender, BleConnectionChangedEventArgs e)
        {
            ConnectionStateChanged?.Invoke(
                this,
                new CommunicationStateChangedEventArgs(e.IsConnected, e.Reason));
        }

        private void OnBleDataReceived(object? sender, BleDataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(e.Data));
        }

        private void RaiseError(string message, Exception? exception = null)
        {
            ErrorOccurred?.Invoke(this, new CommunicationErrorEventArgs(message, exception));
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
            _ble.ConnectionChanged -= OnBleConnectionChanged;
            _ble.DataReceived -= OnBleDataReceived;
        }
    }
}
