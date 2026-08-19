using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrainZoneMultichannel.FrameWork.NRF52840
{
    public sealed class CDCConnector : IDisposable
    {
        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private SerialPortStream? _serialPort;
        private CancellationTokenSource? _receiveCancellation;
        private Task? _receiveTask;
        private bool _disposed;

        public string? PortName { get; private set; }
        public CDCOptions? Options { get; private set; }
        public bool IsOpen => _serialPort?.IsOpen ?? false;

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<byte[]>? BytesReceived;
        public event Action<string>? TextReceived;
        public event Action<CDCErrorCode, string>? ErrorOccurred;

        public static string[] GetPortNames()
        {
            using var serialPort = new SerialPortStream();
            return serialPort.GetPortNames();
        }

        public async Task<bool> OpenAsync(CDCOptions options)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(options);

            await _syncLock.WaitAsync();
            try
            {
                if (IsOpen)
                {
                    RaiseError(CDCErrorCode.PortAlreadyOpen, $"串口 {PortName} 已打开");
                    return false;
                }

                Options = options;
                PortName = options.PortName;

                var port = new SerialPortStream(
                    options.PortName,
                    options.BaudRate,
                    options.DataBits,
                    options.Parity,
                    options.StopBits)
                {
                    ReadTimeout = options.ReadTimeout,
                    WriteTimeout = options.WriteTimeout,
                    ReadBufferSize = options.ReadBufferSize,
                    WriteBufferSize = options.WriteBufferSize,
                    DtrEnable = options.DtrEnable,
                    RtsEnable = options.RtsEnable
                };

                port.Open();
                _serialPort = port;
                _receiveCancellation = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCancellation.Token));

                Logger.Info($"[CDC] 串口 {PortName} 已连接");
                Connected?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                CleanupPort();
                RaiseError(CDCErrorCode.OpenFailed, $"打开串口 {options.PortName} 失败: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task CloseAsync()
        {
            await CloseInternalAsync(true);
        }

        public async Task SendBytesAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (data == null || data.Length == 0)
            {
                return;
            }

            await _syncLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsOpen || _serialPort == null)
                {
                    RaiseError(CDCErrorCode.PortNotOpen, $"串口 {PortName} 未打开");
                    return;
                }

                await _serialPort.WriteAsync(data, 0, data.Length);
                await _serialPort.FlushAsync(cancellationToken);
                Logger.Debug($"[CDC] 发送 {data.Length} 字节: {BitConverter.ToString(data)}");
            }
            catch (Exception ex)
            {
                RaiseError(CDCErrorCode.SendFailed, $"发送数据到串口 {PortName} 失败: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public Task SendTextAsync(string text, CancellationToken cancellationToken = default)
        {
            var encoding = Options?.TextEncoding ?? Encoding.UTF8;
            return SendBytesAsync(encoding.GetBytes(text ?? string.Empty), cancellationToken);
        }

        public Task SendLineAsync(string text, CancellationToken cancellationToken = default)
        {
            return SendTextAsync((text ?? string.Empty) + Environment.NewLine, cancellationToken);
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var bufferSize = Math.Max(256, Options?.ReceiveBufferSize ?? 4096);
            var buffer = new byte[bufferSize];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var port = _serialPort;
                    if (port == null || !port.IsOpen)
                    {
                        break;
                    }

                    int bytesRead = await port.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                    {
                        continue;
                    }

                    var received = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, received, 0, bytesRead);

                    BytesReceived?.Invoke(received);

                    if (TextReceived != null)
                    {
                        var encoding = Options?.TextEncoding ?? Encoding.UTF8;
                        TextReceived.Invoke(encoding.GetString(received));
                    }
                }
                catch (TimeoutException)
                {
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (IOException ex)
                {
                    RaiseError(CDCErrorCode.ReceiveFailed, $"串口 {PortName} 连接中断: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    RaiseError(CDCErrorCode.ReceiveFailed, $"读取串口 {PortName} 数据失败: {ex.Message}");
                    break;
                }
            }

            await CloseInternalAsync(false);
        }

        private async Task CloseInternalAsync(bool reportCloseError)
        {
            if (_disposed)
            {
                return;
            }

            Task? receiveTaskToWait = null;
            bool calledFromReceiveTask = false;

            await _syncLock.WaitAsync();
            try
            {
                if (_serialPort == null)
                {
                    return;
                }

                receiveTaskToWait = _receiveTask;
                calledFromReceiveTask = receiveTaskToWait != null && Task.CurrentId == receiveTaskToWait.Id;
                _receiveCancellation?.Cancel();

                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }

                    Logger.Info($"[CDC] 串口 {PortName} 已关闭");
                    Disconnected?.Invoke();
                }
                catch (Exception ex) when (reportCloseError)
                {
                    RaiseError(CDCErrorCode.CloseFailed, $"关闭串口 {PortName} 失败: {ex.Message}");
                }
                finally
                {
                    CleanupPort();
                }
            }
            finally
            {
                _syncLock.Release();
            }

            if (receiveTaskToWait != null && !calledFromReceiveTask)
            {
                try
                {
                    await receiveTaskToWait;
                }
                catch
                {
                }
            }
        }

        private void CleanupPort()
        {
            _receiveCancellation?.Dispose();
            _receiveCancellation = null;
            _receiveTask = null;
            _serialPort?.Dispose();
            _serialPort = null;
        }

        private void RaiseError(CDCErrorCode code, string message)
        {
            Logger.Error($"[CDC] {message}");
            ErrorOccurred?.Invoke(code, message);
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
            _receiveCancellation?.Cancel();
            CleanupPort();
            _syncLock.Dispose();
        }
    }
}
