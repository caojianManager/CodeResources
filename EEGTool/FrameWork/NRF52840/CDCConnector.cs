using FrameWork.Log;
using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BrainZoneMultichannel.FrameWork.NRF52840
{
    public sealed class CDCConnector : IDisposable
    {
        private const int MaxLoggedBytes = 64;

        private readonly SemaphoreSlim _syncLock = new(1, 1);
        private SerialPortStream? _serialPort;
        private CancellationTokenSource? _receiveCancellation;
        private Task? _receiveTask;
        private Task? _dispatchTask;
        private Channel<byte[]>? _receiveChannel;
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

            string? errorMessage = null;

            await _syncLock.WaitAsync();
            try
            {
                if (IsOpen)
                {
                    errorMessage = $"串口 {PortName} 已打开";
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
                _receiveChannel = CreateReceiveChannel(options);
                _dispatchTask = Task.Run(() => DispatchLoopAsync(_receiveChannel.Reader));
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCancellation.Token));

                Logger.Info($"[CDC] 串口 {PortName} 已连接");
                return true;
            }
            catch (Exception ex)
            {
                CleanupPort();
                errorMessage = $"打开串口 {options.PortName} 失败: {ex.Message}";
                return false;
            }
            finally
            {
                _syncLock.Release();

                if (errorMessage != null)
                {
                    var errorCode = errorMessage.Contains("已打开", StringComparison.Ordinal)
                        ? CDCErrorCode.PortAlreadyOpen
                        : CDCErrorCode.OpenFailed;
                    RaiseError(errorCode, errorMessage);
                }
                else if (IsOpen)
                {
                    InvokeSafely(Connected, "串口连接事件处理失败");
                }
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

                await _serialPort.WriteAsync(data, 0, data.Length, cancellationToken);
                await _serialPort.FlushAsync(cancellationToken);
                Logger.Debug($"[CDC] 发送 {data.Length} 字节: {FormatBytesForLog(data)}");
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

                    var writer = _receiveChannel?.Writer;
                    if (writer == null)
                    {
                        break;
                    }

                    await writer.WriteAsync(received, token);
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

            await CloseInternalAsync(false, false);
        }

        private async Task DispatchLoopAsync(ChannelReader<byte[]> reader)
        {
            await foreach (var received in reader.ReadAllAsync())
            {
                InvokeSafely(BytesReceived, received, "串口数据事件处理失败");

                if (TextReceived != null)
                {
                    var encoding = Options?.TextEncoding ?? Encoding.UTF8;
                    InvokeSafely(TextReceived, encoding.GetString(received), "串口文本事件处理失败");
                }
            }
        }

        private async Task CloseInternalAsync(bool reportCloseError, bool waitForReceiveTask = true)
        {
            if (_disposed)
            {
                return;
            }

            Task? receiveTaskToWait = null;
            Task? dispatchTaskToWait = null;
            bool disconnected = false;
            string? closedPortName = null;

            await _syncLock.WaitAsync();
            try
            {
                if (_serialPort == null)
                {
                    return;
                }

                receiveTaskToWait = _receiveTask;
                dispatchTaskToWait = _dispatchTask;
                closedPortName = PortName;
                _receiveCancellation?.Cancel();
                _receiveChannel?.Writer.TryComplete();

                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }

                    Logger.Info($"[CDC] 串口 {PortName} 已关闭");
                    disconnected = true;
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

            if (disconnected)
            {
                InvokeSafely(Disconnected, $"串口 {closedPortName} 断开事件处理失败");
            }

            if (waitForReceiveTask && receiveTaskToWait != null)
            {
                try
                {
                    await receiveTaskToWait;
                }
                catch
                {
                }
            }

            if (waitForReceiveTask && dispatchTaskToWait != null)
            {
                try
                {
                    await dispatchTaskToWait;
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
            _dispatchTask = null;
            _receiveChannel = null;
            _serialPort?.Dispose();
            _serialPort = null;
        }

        private void RaiseError(CDCErrorCode code, string message)
        {
            Logger.Error($"[CDC] {message}");
            ErrorOccurred?.Invoke(code, message);
        }

        private void InvokeSafely(Action? handler, string errorMessage)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler.Invoke();
            }
            catch (Exception ex)
            {
                RaiseError(CDCErrorCode.ReceiveFailed, $"{errorMessage}: {ex.Message}");
            }
        }

        private void InvokeSafely<T>(Action<T>? handler, T value, string errorMessage)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler.Invoke(value);
            }
            catch (Exception ex)
            {
                RaiseError(CDCErrorCode.ReceiveFailed, $"{errorMessage}: {ex.Message}");
            }
        }

        private static Channel<byte[]> CreateReceiveChannel(CDCOptions options)
        {
            var capacity = Math.Max(1, options.ReceiveQueueCapacity);
            var fullMode = options.DropOldestWhenReceiveQueueFull
                ? BoundedChannelFullMode.DropOldest
                : BoundedChannelFullMode.Wait;

            return Channel.CreateBounded<byte[]>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = fullMode
            });
        }

        private static string FormatBytesForLog(byte[] data)
        {
            if (data.Length <= MaxLoggedBytes)
            {
                return BitConverter.ToString(data);
            }

            var preview = new byte[MaxLoggedBytes];
            Buffer.BlockCopy(data, 0, preview, 0, MaxLoggedBytes);
            return $"{BitConverter.ToString(preview)}... (+{data.Length - MaxLoggedBytes} 字节)";
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
            _receiveChannel?.Writer.TryComplete();
            CleanupPort();
            _syncLock.Dispose();
        }
    }
}
