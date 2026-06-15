using BLETool;
using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
using EEGTool.Models.Impedance;
using EEGTool.Models;
using Framework.Event;
using FrameWork.Event;
using FrameWork.Log;
using FrameWork.MVVM;
using FrameWork.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using CommandManager = EEGTool.Models.BLE.CommandManager;

namespace EEGTool.ViewModels.Impedance
{
    public class ImpedanceHomeViewModel : BindableBase
    {
        private readonly BleManager _ble = BleToolKit.Shared;
        private readonly BleCommandStreamParser _commandStreamParser = new();
        private readonly SemaphoreSlim _monitorLock = new(1, 1);
        private BleGattCharacteristicInfo? _writeCharacteristic;
        private BleGattCharacteristicInfo? _notifyCharacteristic;
        private bool _isNotifySubscribed;
        private bool _isDataReceivedSubscribed;
        private bool _isMonitorRunning;
        private bool _isVisible;
        private TaskCompletionSource<bool>? _configureImpedanceCompletion;
        private MultiChannelRingBuffer? _dataBuffer;
        private DataProcessor? _dataProcessor;
        private readonly object _dataProcessingLock = new();
        private readonly Queue<float[]> _pendingSamples = new();
        private HighPrecisionTimer? _monitorTimer;
        private CancellationTokenSource? _samplePumpCts;
        private Task? _samplePumpTask;
        private double _samplePumpRemainder;
        private long _samplesWrittenVersion;
        private long _lastProcessedSamplesVersion;
        private const int ConfigureImpedanceTimeoutMilliseconds = 3000;
        private const int MonitorTimerIntervalMilliseconds = 40;
        private const int SamplePumpIntervalMilliseconds = 10;
        private const int TargetPendingLatencyMilliseconds = 100;
        private const int MaxPendingLatencyMilliseconds = 500;

        public bool IsMonitorRunning
        {
            get => _isMonitorRunning;
            private set => SetProperty(ref _isMonitorRunning, value);
        }

        public void OnShow()
        {
            _isVisible = true;
            SubscribeDataReceived();
            _ = StartMonitorAsync();
        }

        public void OnHide()
        {
            _isVisible = false;
            _ = StopMonitorAsync();
        }

        private async Task StartMonitorAsync()
        {
            await _monitorLock.WaitAsync();
            try
            {
                if (!_isVisible || IsMonitorRunning)
                {
                    return;
                }

                if (!await GetGattProfileAsync())
                {
                    return;
                }

                var collectionInfo = CollectionInfoManager.GetInstance().Info;
                _configureImpedanceCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                byte[] configureCommand = ImpedanceCommandBuilder.BuildConfigureCommand(collectionInfo);
                Logger.Info($"[ImpedanceHomeViewModel][StartMonitorAsync]:阻抗配置指令 {CommandManager.ToHexString(configureCommand)}");
                await WriteDataToBleAsync(configureCommand);

                Task completedTask = await Task.WhenAny(
                    _configureImpedanceCompletion.Task,
                    Task.Delay(ConfigureImpedanceTimeoutMilliseconds));
                if (completedTask != _configureImpedanceCompletion.Task)
                {
                    Logger.Debug("[ImpedanceHomeViewModel][StartMonitorAsync]:等待阻抗配置响应超时");
                    ShowMessage("阻抗配置指令已发送，但设备未在3秒内响应。", "阻抗配置超时");
                    return;
                }

                if (!await _configureImpedanceCompletion.Task)
                {
                    Logger.Debug("[ImpedanceHomeViewModel][StartMonitorAsync]:设备返回阻抗配置失败");
                    ShowMessage("设备返回阻抗配置失败，未发送开始阻抗监测指令。", "阻抗配置失败");
                    return;
                }

                if (!_isVisible)
                {
                    return;
                }

                byte[] startCommand = CommandManager.BuildStartImpedanceMonitorCommand();
                Logger.Info($"[ImpedanceHomeViewModel][StartMonitorAsync]:开始阻抗监测指令 {CommandManager.ToHexString(startCommand)}");
                await WriteDataToBleAsync(startCommand);
                IsMonitorRunning = true;
                StartMonitorTimer();
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ImpedanceHomeViewModel][StartMonitorAsync]:启动阻抗监测失败 {ex}");
                ShowBleError(ex, "启动阻抗监测失败");
            }
            finally
            {
                _configureImpedanceCompletion = null;
                _monitorLock.Release();
            }
        }

        private async Task StopMonitorAsync()
        {
            await _monitorLock.WaitAsync();
            try
            {
                if (IsMonitorRunning && _writeCharacteristic != null)
                {
                    byte[] stopCommand = CommandManager.BuildStopImpedanceCommand();
                    Logger.Info($"[ImpedanceHomeViewModel][StopMonitorAsync]:停止阻抗监测指令 {CommandManager.ToHexString(stopCommand)}");
                    await WriteDataToBleAsync(stopCommand);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ImpedanceHomeViewModel][StopMonitorAsync]:停止阻抗监测失败 {ex}");
            }
            finally
            {
                IsMonitorRunning = false;
                StopMonitorTimer();
                _commandStreamParser.Clear();
                UnsubscribeDataReceived();
                _monitorLock.Release();
            }
        }

        private void SubscribeDataReceived()
        {
            if (_isDataReceivedSubscribed)
            {
                return;
            }

            _ble.DataReceived += DataReceived;
            _isDataReceivedSubscribed = true;
        }

        private void UnsubscribeDataReceived()
        {
            if (!_isDataReceivedSubscribed)
            {
                return;
            }

            _ble.DataReceived -= DataReceived;
            _isDataReceivedSubscribed = false;
        }

        private void DataReceived(object? sender, BleDataReceivedEventArgs e)
        {
            Logger.Debug($"[ImpedanceHomeViewModel][DataReceived]:收到原始数据 {CommandManager.ToHexString(e.Data)}");

            IReadOnlyList<CommandParseResult> results = _commandStreamParser.Push(e.Data);
            foreach (var result in results)
            {
                HandleCommandResult(result);
            }
        }

        private void HandleCommandResult(CommandParseResult result)
        {
            if (!result.IsSuccess)
            {
                Logger.Debug($"[ImpedanceHomeViewModel][DataReceived]:解析失败 {result.Status} {result.Message}");
                return;
            }

            if (result.Response?.CommandType == BleCommandType.StopImpedanceResponse)
            {
                Logger.Info($"[ImpedanceHomeViewModel][DataReceived]:停止阻抗响应 Status={result.Response.StatusCode}, Detail={result.Response.ErrorDetail}");
                if (result.Response.IsSuccess)
                {
                    IsMonitorRunning = false;
                }
            }

            if (result.Response?.CommandType == BleCommandType.ConfigureImpedanceResponse)
            {
                Logger.Info($"[ImpedanceHomeViewModel][DataReceived]:阻抗配置响应 Status={result.Response.StatusCode}, Detail={result.Response.ErrorDetail}");
                _configureImpedanceCompletion?.TrySetResult(result.Response.IsSuccess);
                return;
            }

            if (result.Battery != null)
            {
                Logger.Info($"[ImpedanceHomeViewModel][DataReceived]:收到电量 {result.Battery.ElectricityQuantity}");
            }

            DataFrame? dataFrame = result.DataFrame;
            if (dataFrame == null || dataFrame.CommandType != BleCommandType.ImpedanceMonitorData)
            {
                return;
            }

            Logger.Debug($"[ImpedanceHomeViewModel][DataReceived]:收到阻抗数据 {dataFrame.CommandType}, Channels={dataFrame.ChannelCount}, Samples={dataFrame.SampleCount}, Battery={dataFrame.ElectricityQuantity}");
            _ = ReceivedImpedanceData(dataFrame);
        }

        private async Task<bool> GetGattProfileAsync()
        {
            if (_writeCharacteristic != null && _notifyCharacteristic != null && _isNotifySubscribed)
            {
                return true;
            }

            var dataChannel = await BleGattProfileHelper.GetDataChannelAsync(_ble);
            if (dataChannel == null)
            {
                Logger.Debug("[ImpedanceHomeViewModel][GetGattProfileAsync]:没有找到阻抗监测需要的写入或通知特征");
                return false;
            }

            _writeCharacteristic = dataChannel.WriteCharacteristic;
            _notifyCharacteristic = dataChannel.NotifyCharacteristic;
            _isNotifySubscribed = dataChannel.IsNotifySubscribed;
            Logger.Info($"[ImpedanceHomeViewModel][GetGattProfileAsync]:Notify订阅成功 Service={_notifyCharacteristic.ServiceUuid}, Characteristic={_notifyCharacteristic.Uuid}");
            return true;
        }

        private async Task ReceivedImpedanceData(DataFrame dataFrame)
        {
            EnsureDataProcessor(dataFrame);
            if (_dataBuffer == null || _dataProcessor == null)
            {
                Logger.Debug("[ImpedanceHomeViewModel][ReceivedImpedanceData]:数据处理器初始化失败");
                return;
            }

            float[][] samples = ConvertDataFrameSamples(dataFrame);
            int droppedSamples = 0;
            int pendingCount;
            int bufferCount;
            lock (_dataProcessingLock)
            {
                foreach (float[] sample in samples)
                {
                    _pendingSamples.Enqueue(sample);
                }

                int maxPendingSamples = Math.Max(
                    1,
                    _dataBuffer.SampleRate * MaxPendingLatencyMilliseconds / 1000);
                while (_pendingSamples.Count > maxPendingSamples)
                {
                    _pendingSamples.Dequeue();
                    droppedSamples++;
                }

                pendingCount = _pendingSamples.Count;
                bufferCount = _dataBuffer.Count;
                EnsureSamplePumpRunning();
            }

            if (droppedSamples > 0)
            {
                Logger.Info($"[ImpedanceHomeViewModel][ReceivedImpedanceData]:平滑队列超出实时延迟上限，丢弃旧样本 Count={droppedSamples}");
            }

            Logger.Debug($"[ImpedanceHomeViewModel][ReceivedImpedanceData]:阻抗数据已进入平滑队列 Pending={pendingCount}, BufferCount={bufferCount}");
            await Task.CompletedTask;
        }

        private void EnsureDataProcessor(DataFrame dataFrame)
        {
            int channelCount = dataFrame.ChannelCount > 0 ? dataFrame.ChannelCount : 16;
            int sampleRate = CollectionInfoManager.GetInstance().Info.SampleRate > 0
                ? CollectionInfoManager.GetInstance().Info.SampleRate
                : 250;

            if (_dataBuffer != null &&
                _dataProcessor != null &&
                _dataBuffer.ChannelCount == channelCount &&
                _dataBuffer.SampleRate == sampleRate)
            {
                return;
            }

            _dataBuffer = new MultiChannelRingBuffer(channelCount, sampleRate);
            _dataProcessor = new DataProcessor(
                _dataBuffer,
                new PassthroughSignalFilter(),
                new MathNetFftProcessor(),
                new DataProcessorSettings(channelCount, sampleRate));
            _pendingSamples.Clear();
            _samplePumpRemainder = 0;
            _samplesWrittenVersion = 0;
            _lastProcessedSamplesVersion = 0;

            Logger.Info($"[ImpedanceHomeViewModel][EnsureDataProcessor]:初始化数据处理器 Channels={channelCount}, SampleRate={sampleRate}");
        }

        private void StartMonitorTimer()
        {
            if (_monitorTimer?.IsRunning == true)
            {
                return;
            }

            _monitorTimer ??= new HighPrecisionTimer(
                TimeSpan.FromMilliseconds(MonitorTimerIntervalMilliseconds),
                OnMonitorTimerTick,
                ex => Logger.Debug($"[ImpedanceHomeViewModel][StartMonitorTimer]:监测定时器异常 {ex}"));
            _monitorTimer.Start();
        }

        private void StopMonitorTimer()
        {
            _monitorTimer?.Stop();
            StopSamplePump();
        }

        private void EnsureSamplePumpRunning()
        {
            if (_samplePumpTask?.IsCompleted == false)
            {
                return;
            }

            _samplePumpCts?.Dispose();
            _samplePumpCts = new CancellationTokenSource();
            _samplePumpTask = Task.Run(() => PumpSamplesAsync(_samplePumpCts.Token));
        }

        private void StopSamplePump()
        {
            _samplePumpCts?.Cancel();
            _samplePumpCts?.Dispose();
            _samplePumpCts = null;
            _samplePumpTask = null;
            _samplePumpRemainder = 0;

            lock (_dataProcessingLock)
            {
                _pendingSamples.Clear();
            }
        }

        private async Task PumpSamplesAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(SamplePumpIntervalMilliseconds));
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    lock (_dataProcessingLock)
                    {
                        if (_dataBuffer == null || _pendingSamples.Count == 0)
                        {
                            return;
                        }

                        double samplesPerTick = _dataBuffer.SampleRate * SamplePumpIntervalMilliseconds / 1000.0;
                        _samplePumpRemainder += samplesPerTick;
                        int targetPendingSamples = Math.Max(
                            1,
                            _dataBuffer.SampleRate * TargetPendingLatencyMilliseconds / 1000);
                        int catchUpSamples = Math.Max(0, _pendingSamples.Count - targetPendingSamples);
                        int samplesToWrite = Math.Min(
                            _pendingSamples.Count,
                            (int)_samplePumpRemainder + catchUpSamples);
                        if (samplesToWrite <= 0)
                        {
                            continue;
                        }

                        int scheduledSamples = Math.Min(samplesToWrite, (int)_samplePumpRemainder);
                        _samplePumpRemainder -= scheduledSamples;
                        for (int i = 0; i < samplesToWrite; i++)
                        {
                            _dataBuffer.AddSample(_pendingSamples.Dequeue());
                            _samplesWrittenVersion++;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnMonitorTimerTick(HighPrecisionTimerTick tick)
        {
            DataProcessingResult result;
            int bufferCount;
            lock (_dataProcessingLock)
            {
                if (_dataBuffer == null ||
                    _dataProcessor == null ||
                    _dataBuffer.Count == 0 ||
                    _samplesWrittenVersion == _lastProcessedSamplesVersion)
                {
                    return;
                }

                result = _dataProcessor.Process();
                bufferCount = _dataBuffer.Count;
                _lastProcessedSamplesVersion = _samplesWrittenVersion;
            }

            EventUtilManager.EventUitl.OnEvent<DataProcessingResult>(
                EventName.RECEVIED_IMPEDANCE_DATA,
                result);
            Logger.Debug($"[ImpedanceHomeViewModel][OnMonitorTimerTick]:阻抗数据处理完成 Tick={tick.TickIndex}, BufferCount={bufferCount}, ReferenceChannel={result.ReferenceChannel}");
        }

        private static float[][] ConvertDataFrameSamples(DataFrame dataFrame)
        {
            int channelCount = dataFrame.Samples.Count;
            int sampleCount = dataFrame.SampleCount;
            var samples = new float[sampleCount][];

            for (int sample = 0; sample < sampleCount; sample++)
            {
                samples[sample] = new float[channelCount];
                for (int channel = 0; channel < channelCount; channel++)
                {
                    uint adcValue = (uint)dataFrame.Samples[channel][sample] & 0x00FFFFFF;
                    samples[sample][channel] = CalculateAdcMicrovolts(adcValue);
                }
            }

            return samples;
        }

        private static float CalculateAdcMicrovolts(uint value)
        {
            double referenceVoltage = FrameWork.Common.Config.Instance.ReferenceVoltage;
            double gain = FrameWork.Common.Config.Instance.GainNum;
            if (gain <= 0)
            {
                gain = 1;
            }

            double microvolts = (value & 0x00800000) != 0
                ? -1 * ((16777216 - (double)value) * referenceVoltage / 0x7FFFFF * 1000 / gain * 1000)
                : (double)value * referenceVoltage / 0x7FFFFF * 1000 / gain * 1000;
            return (float)Math.Round(microvolts, 6);
        }

        private sealed class PassthroughSignalFilter : ISignalFilter
        {
            public void BandPass(double[] data, int sampleRate, double startHz, double stopHz, int order, FilterKind type) { }
            public void BandStop(double[] data, int sampleRate, double startHz, double stopHz, int order, FilterKind type) { }
            public void RemoveEnvironmentalNoise(double[] data, int sampleRate, int noiseHz) { }
        }

        private sealed class MathNetFftProcessor : IFftProcessor
        {
            public float[] ComputeAmplitudeSpectrum(float[] timeData, int sampleRate)
            {
                if (timeData == null || timeData.Length == 0)
                {
                    return Array.Empty<float>();
                }

                var spectrum = timeData.Select(value => new Complex(value, 0)).ToArray();
                Fourier.Forward(spectrum, FourierOptions.Matlab);

                int resultLength = spectrum.Length / 2 + 1;
                var amplitude = new float[resultLength];
                for (int index = 0; index < resultLength; index++)
                {
                    double scale = index == 0 ||
                        (spectrum.Length % 2 == 0 && index == spectrum.Length / 2)
                        ? 1.0 / spectrum.Length
                        : 2.0 / spectrum.Length;
                    amplitude[index] = (float)(spectrum[index].Magnitude * scale);
                }

                return amplitude;
            }
        }

        private async Task WriteDataToBleAsync(byte[] data)
        {
            if (_writeCharacteristic == null)
            {
                throw new InvalidOperationException("阻抗监测写入特征尚未初始化。");
            }

            await _ble.WriteAsync(_writeCharacteristic.ServiceUuid, _writeCharacteristic.Uuid, data);
            Logger.Info($"[ImpedanceHomeViewModel][WriteDataToBleAsync]:发送成功 {CommandManager.ToHexString(data)}");
        }

        private static void ShowBleError(Exception ex, string title)
        {
            bool accessDenied = ex is BleAccessDeniedException
                || ex is UnauthorizedAccessException
                || (ex is BleException bleException && bleException.Message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase));

            string message = accessDenied
                ? "当前蓝牙设备已连接，但本软件没有读取或写入权限。请关闭其他蓝牙工具，断开设备后在本软件中重新连接。"
                : ex.Message;

            Application.Current?.Dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning));
        }

        private static void ShowMessage(string message, string title)
        {
            Application.Current?.Dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning));
        }
    }
}
