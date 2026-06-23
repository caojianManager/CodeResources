using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Event;
using FrameWork.MVVM;
using FrameWork.Tools;
using MathNet.Numerics.IntegralTransforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BLETool;
using EEGTool.Models;
using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
using EEGTool.Models.Impedance;
using EEGTool.Models.Template;
using CommandManager = EEGTool.Models.BLE.CommandManager;
using Logger = FrameWork.Log.Logger;

namespace EEGTool.ViewModels
{

    public class CollectionMonitorViewModel : BindableBase,IApplicationContentView
    {

        public string Name => "采集监测页面";
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isInit = false;
        public bool IsInit
        {
            get => _isInit;
            set => SetProperty(ref _isInit, value);
        }

        private BleManager _ble;
        private BleGattCharacteristicInfo _writeCharacteristic, _notifyCharacteristic;
        private readonly BleCommandStreamParser _commandStreamParser = new();
        private bool _isNotifySubscribed;
        private MultiChannelRingBuffer? _dataBuffer;
        private DataProcessor? _dataProcessor;
        private DataProcessingResult? _latestProcessingResult;
        private readonly object _dataProcessingLock = new();
        private readonly Queue<float[]> _pendingSamples = new();
        private HighPrecisionTimer? _monitorTimer;
        private CancellationTokenSource? _samplePumpCts;
        private Task? _samplePumpTask;
        private double _samplePumpRemainder;
        private long _samplesWrittenVersion;
        private long _lastProcessedSamplesVersion;
        private TaskCompletionSource<bool>? _stopCollectionCompletion;
        private TaskCompletionSource<bool>? _configureImpedanceCompletion;
        private bool _isSwitchingToImpedance;
        private const int MonitorTimerIntervalMilliseconds = 40;
        private const int SamplePumpIntervalMilliseconds = 10;
        private const int TargetPendingLatencyMilliseconds = 100;
        private const int StopCollectionTimeoutMilliseconds = 3000;
        private const int ConfigureImpedanceTimeoutMilliseconds = 3000;

        public ICommand? BackHomeCommand { get; set; }
        public ICommand? ShowImpedanceCommand { get; set; }
        public ICommand? ShowMonitorCommand { get; set; }
        public ICommand? StartRecordCommand { get; set; }

        private bool _isShowMonitor = true;
        public bool IsShowMonitor
        {
            get => _isShowMonitor;
            set => SetProperty(ref _isShowMonitor, value);
        }

        private bool _isDeviceConnectTabSelected = true;
        public bool IsDeviceConnectTabSelected
        {
            get => _isDeviceConnectTabSelected;
            set
            {
                if (SetProperty(ref _isDeviceConnectTabSelected, value))
                {
                    OnPropertyChanged(nameof(IsCollectionConfigTabSelected));
                }
            }
        }

        public bool IsCollectionConfigTabSelected
        {
            get => !IsDeviceConnectTabSelected;
            set => IsDeviceConnectTabSelected = !value;
        }

        public void Init()
        {
            Config();
        }

        private void Config()
        {
            _ble = BleToolKit.Shared;
            _ble.DataReceived += DataReceived;
            BackHomeCommand = new RelayCommand((o) =>
            {
                var result = MessageBox.Show(
                    $"确定要结束采集吗？",
                    "结束采集",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.OK)
                {
                    return;
                }

                EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(MainViewModel));
            });

            ShowImpedanceCommand = new RelayCommand(_ => _ = SwitchToImpedanceAsync());
            ShowMonitorCommand = new RelayCommand(_ => IsShowMonitor = true);

            
        }

        /// <summary>
        /// 蓝牙接收到数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataReceived(object? sender, BleDataReceivedEventArgs e)
        {
            Logger.Debug($"[CollectionMonitorViewModel][DataReceived]:收到原始数据 {CommandManager.ToHexString(e.Data)}");

            var results = _commandStreamParser.Push(e.Data);
            foreach (var result in results)
            {
                HandleCommandResult(result);
            }
        }

        /// <summary>
        /// 开始监测
        /// </summary>
        private async Task StartMonitor()
        {
            //1.获取采集配置信息
            var cInfo = CollectionInfoManager.GetInstance().Info;
            if (cInfo.ConfigureCommandSent)
            {
                Logger.Info("[CollectionMonitorViewModel][StartMonitor]:采集配置指令已在采集配置页发送，跳过重复发送");
                return;
            }

            //2.配置采集指令-并发送给下位机(MCU);
            byte[] command = CollectionCommandBuilder.BuildConfigureCommand(cInfo);
            Logger.Info($"[CollectionMonitorViewModel][StartMonitor]:采集配置指令 {CommandManager.ToHexString(command)}");
            await WriteDataToBLE(command);
        }


        /// <summary>
        /// 停止监测
        /// </summary>
        private void StopMonitor()
        {
            StopMonitorTimer();
        }

        private async Task SwitchToImpedanceAsync()
        {
            if (_isSwitchingToImpedance || !IsShowMonitor)
            {
                return;
            }

            _isSwitchingToImpedance = true;
            try
            {
                if (!await GetGattProfile())
                {
                    MessageBox.Show(
                        "没有找到可用的蓝牙写入通道，无法停止采集。",
                        "切换阻抗监测失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _stopCollectionCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                byte[] command = CommandManager.BuildStopCollectionCommand();
                Logger.Info($"[CollectionMonitorViewModel][SwitchToImpedanceAsync]:停止采集指令 {CommandManager.ToHexString(command)}");
                await WriteDataToBLE(command);

                Task completedTask = await Task.WhenAny(
                    _stopCollectionCompletion.Task,
                    Task.Delay(StopCollectionTimeoutMilliseconds));

                if (completedTask != _stopCollectionCompletion.Task)
                {
                    Logger.Debug("[CollectionMonitorViewModel][SwitchToImpedanceAsync]:等待停止采集响应超时");
                    MessageBox.Show(
                        "停止采集指令已发送，但设备未在3秒内响应，暂不启动阻抗监测。",
                        "停止采集超时",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!await _stopCollectionCompletion.Task)
                {
                    MessageBox.Show(
                        "设备返回停止采集失败，暂不启动阻抗监测。",
                        "停止采集失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                CollectionInfo collectionInfo = CollectionInfoManager.GetInstance().Info;
                collectionInfo.ImpedanceConfigureCommandConfirmed = false;
                _configureImpedanceCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                byte[] configureImpedanceCommand = ImpedanceCommandBuilder.BuildConfigureCommand(collectionInfo);
                Logger.Info($"[CollectionMonitorViewModel][SwitchToImpedanceAsync]:阻抗配置指令 {CommandManager.ToHexString(configureImpedanceCommand)}");
                await WriteDataToBLE(configureImpedanceCommand);

                completedTask = await Task.WhenAny(
                    _configureImpedanceCompletion.Task,
                    Task.Delay(ConfigureImpedanceTimeoutMilliseconds));
                if (completedTask != _configureImpedanceCompletion.Task)
                {
                    Logger.Debug("[CollectionMonitorViewModel][SwitchToImpedanceAsync]:等待阻抗配置响应超时");
                    MessageBox.Show(
                        "设备异常，请检查。",
                        "阻抗监测异常",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!await _configureImpedanceCompletion.Task)
                {
                    Logger.Debug("[CollectionMonitorViewModel][SwitchToImpedanceAsync]:设备返回阻抗配置失败");
                    MessageBox.Show(
                        "设备异常，请检查。",
                        "阻抗监测异常",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                collectionInfo.ImpedanceConfigureCommandConfirmed = true;
                StopMonitor();
                IsShowMonitor = false;
            }
            catch (Exception ex)
            {
                Logger.Debug($"[CollectionMonitorViewModel][SwitchToImpedanceAsync]:切换阻抗监测失败 {ex}");
                MessageBox.Show(
                    IsBleAccessDenied(ex)
                        ? "当前蓝牙设备没有写入权限，请重新连接设备后再试。"
                        : ex.Message,
                    "切换阻抗监测失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                _stopCollectionCompletion = null;
                _configureImpedanceCompletion = null;
                _isSwitchingToImpedance = false;
            }
        }

        private void ClickPlaybackBtn()
        {

        }

        private void ClickCollectionBtn()
        {

        }

        public void OnHide()
        {
            StopMonitorTimer();
        }

        public void OnShow()
        {
            IsShowMonitor = true;
            _ = OnShowAsync();
        }

        private async Task OnShowAsync()
        {
            try
            {
                if (!await GetGattProfile())
                {
                    return;
                }

                await StartMonitor();
            }
            catch (Exception ex)
            {
                Logger.Debug($"[CollectionMonitorViewModel][OnShowAsync]:启动采集监测失败 {ex}");
                if (IsBleAccessDenied(ex))
                {
                    MessageBox.Show(
                        "当前蓝牙设备已连接，但本软件没有读取或写入权限，可能被其他软件持有。\n\n请关闭第三方蓝牙工具，或在系统蓝牙中断开后，回到本软件的蓝牙连接页面重新连接。",
                        "蓝牙设备被占用",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private async Task<bool> GetGattProfile()
        {
            if (_ble == null)
            {
                Logger.Debug("[CollectionMonitorViewModel][GetGattProfile]:蓝牙管理器为空~");
                return false;
            }

            if (_writeCharacteristic != null && _notifyCharacteristic != null && _isNotifySubscribed)
            {
                return true;
            }

            var dataChannel = await BleGattProfileHelper.GetDataChannelAsync(
                _ble,
                continueWhenNotifyAccessDenied: true);
            if (dataChannel == null)
            {
                return false;
            }

            _writeCharacteristic = dataChannel.WriteCharacteristic;
            _notifyCharacteristic = dataChannel.NotifyCharacteristic;
            _isNotifySubscribed = dataChannel.IsNotifySubscribed;

            Logger.Info($"[CollectionMonitorViewModel][GetGattProfile]:Write特征 Service={_writeCharacteristic.ServiceUuid}, Characteristic={_writeCharacteristic.Uuid}, Properties={_writeCharacteristic.Properties}");
            return true;
        }

        private async Task WriteDataToBLE(byte[] data)
        {
            if (_ble == null || _writeCharacteristic == null)
            {
                Logger.Debug("[CollectionMonitorViewModel][WriteDataToBLE]:蓝牙或写入特征为空，发送失败~");
                return;
            }

            try
            {
                await _ble.WriteAsync(_writeCharacteristic.ServiceUuid, _writeCharacteristic.Uuid, data);
                Logger.Info($"[CollectionMonitorViewModel][WriteDataToBLE]:发送成功 {CommandManager.ToHexString(data)}");
            }
            catch (BleAccessDeniedException ex)
            {
                Logger.Debug($"[CollectionMonitorViewModel][WriteDataToBLE]:写入访问被拒绝。Service={_writeCharacteristic.ServiceUuid}, Characteristic={_writeCharacteristic.Uuid}, Properties={_writeCharacteristic.Properties}, Data={CommandManager.ToHexString(data)}, Error={ex}");
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Debug($"[CollectionMonitorViewModel][WriteDataToBLE]:写入无权限。Service={_writeCharacteristic.ServiceUuid}, Characteristic={_writeCharacteristic.Uuid}, Properties={_writeCharacteristic.Properties}, Data={CommandManager.ToHexString(data)}, Error={ex}");
                throw;
            }
        }

        private static bool IsBleAccessDenied(Exception ex)
        {
            return ex is BleAccessDeniedException
                || ex is UnauthorizedAccessException
                || (ex is BleException && ex.Message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
                || (ex.InnerException != null && IsBleAccessDenied(ex.InnerException));
        }

        private void HandleCommandResult(CommandParseResult result)
        {
            if (!result.IsSuccess)
            {
                Logger.Debug($"[CollectionMonitorViewModel][DataReceived]:解析失败 {result.Status} {result.Message}");
                return;
            }

            if (result.Response != null)
            {
                Logger.Info($"[CollectionMonitorViewModel][DataReceived]:收到命令响应 {result.Response.CommandType}, Status={result.Response.StatusCode}, Detail={result.Response.ErrorDetail}");
                _ = HandleResponse(result.Response);
            }

            if (result.Battery != null)
            {
                Logger.Info($"[CollectionMonitorViewModel][DataReceived]:收到电量 {result.Battery.ElectricityQuantity}");
            }

            if (result.DataFrame != null)
            {
                Logger.Debug($"[CollectionMonitorViewModel][DataReceived]:收到数据帧 {result.DataFrame.CommandType}, Channels={result.DataFrame.ChannelCount}, Samples={result.DataFrame.SampleCount}");
                if (result.DataFrame.CommandType == BleCommandType.CollectionData)
                {
                    _ = ReceivedCollectionData(result.DataFrame);
                }
            }

        }

        private async Task HandleResponse(CommandResponse response)
        {
            if (response.CommandType == BleCommandType.StopCollectionResponse)
            {
                Logger.Info($"[CollectionMonitorViewModel][DataReceived]:停止采集响应 Status={response.StatusCode}, Detail={response.ErrorDetail}");
                _stopCollectionCompletion?.TrySetResult(response.IsSuccess);
                return;
            }

            if (response.CommandType == BleCommandType.ConfigureImpedanceResponse)
            {
                Logger.Info($"[CollectionMonitorViewModel][DataReceived]:阻抗配置响应 Status={response.StatusCode}, Detail={response.ErrorDetail}");
                _configureImpedanceCompletion?.TrySetResult(response.IsSuccess);
                return;
            }

            if (response.CommandType == BleCommandType.ConfigureCollectionResponse)
            {
                if (!response.IsSuccess)
                {
                    Logger.Debug($"[CollectionMonitorViewModel][DataReceived]:配置采集失败 Status={response.StatusCode}, Detail={response.ErrorDetail}");
                    return;
                }
                await ReceivedConfigCollection();
                return;
            }

        }

        /// <summary>
        /// 接收采集数据
        /// </summary>
        /// <returns></returns>
        private async Task ReceivedCollectionData(DataFrame dataFrame)
        {
            Logger.Debug($"[CollectionMonitorViewModel][ReceivedCollectionData]:处理采集数据 Channels={dataFrame.ChannelCount}, Samples={dataFrame.SampleCount}, Battery={dataFrame.ElectricityQuantity}");
            EnsureDataProcessor(dataFrame);

            if (_dataBuffer == null || _dataProcessor == null)
            {
                Logger.Debug("[CollectionMonitorViewModel][ReceivedCollectionData]:数据处理器初始化失败");
                return;
            }

            var samples = ConvertDataFrameSamples(dataFrame);
            lock (_dataProcessingLock)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    _pendingSamples.Enqueue(samples[i]);
                }

                EnsureSamplePumpRunning();
            }

            Logger.Debug($"[CollectionMonitorViewModel][ReceivedCollectionData]:采集数据已进入平滑队列 Pending={_pendingSamples.Count}, BufferCount={_dataBuffer.Count}");
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

            Logger.Info($"[CollectionMonitorViewModel][EnsureDataProcessor]:初始化数据处理器 Channels={channelCount}, SampleRate={sampleRate}");
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
                ex => Logger.Debug($"[CollectionMonitorViewModel][StartMonitorTimer]:监测定时器异常 {ex}"));
            _monitorTimer.Start();

            Logger.Info($"[CollectionMonitorViewModel][StartMonitorTimer]:启动监测定时器 Interval={MonitorTimerIntervalMilliseconds}ms");
        }

        private void StopMonitorTimer()
        {
            if (_monitorTimer == null)
            {
                return;
            }

            _monitorTimer.Stop();
            StopSamplePump();
            Logger.Info("[CollectionMonitorViewModel][StopMonitorTimer]:停止监测定时器");
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
                    int targetPendingSamples = Math.Max(1,
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

        private void OnMonitorTimerTick(HighPrecisionTimerTick tick)
        {
            DataProcessingResult? result = null;
            int bufferCount = 0;
            lock (_dataProcessingLock)
            {
                if (_dataBuffer == null ||
                    _dataProcessor == null ||
                    _dataBuffer.Count == 0 ||
                    _samplesWrittenVersion == _lastProcessedSamplesVersion)
                {
                    return;
                }

                _latestProcessingResult = _dataProcessor.Process();
                result = _latestProcessingResult;
                result.TotalSamplesWritten = _samplesWrittenVersion;
                bufferCount = _dataBuffer.Count;
                _lastProcessedSamplesVersion = _samplesWrittenVersion;
            }

            EventUtilManager.EventUitl.OnEvent<DataProcessingResult>(EventName.RECEVIED_COLLECTION_DATA, result);
            Logger.Debug($"[CollectionMonitorViewModel][OnMonitorTimerTick]:定时获取数据完成 Tick={tick.TickIndex}, Drift={tick.Drift.TotalMilliseconds:F3}ms, BufferCount={bufferCount}, ReferenceChannel={result.ReferenceChannel}");
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

            double microvolts;
            if ((value & 0x00800000) != 0)
            {
                microvolts = -1 * ((16777216 - (double)value) * referenceVoltage / 0x7FFFFF * 1000 / gain * 1000);
            }
            else
            {
                microvolts = (double)value * referenceVoltage / 0x7FFFFF * 1000 / gain * 1000;
            }

            return (float)Math.Round(microvolts, 6);
        }

        private sealed class PassthroughSignalFilter : ISignalFilter
        {
            public void BandPass(double[] data, int sampleRate, double startHz, double stopHz, int order, FilterKind type)
            {
            }

            public void BandStop(double[] data, int sampleRate, double startHz, double stopHz, int order, FilterKind type)
            {
            }

            public void RemoveEnvironmentalNoise(double[] data, int sampleRate, int noiseHz)
            {
            }
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

        private async Task ReceivedConfigCollection()
        {
            //收到配置采集成功的回复
            //开始采集指令-并发送给下位机(MCU);
            byte[] command = CommandManager.BuildStartCollectionCommand();
            Logger.Info($"[CollectionMonitorViewModel][ReceivedConfigCollection]:开始采集指令 {CommandManager.ToHexString(command)}");
            await WriteDataToBLE(command);
            StartMonitorTimer();
        }

    }
}
