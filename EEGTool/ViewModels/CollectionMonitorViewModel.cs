using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Event;
using FrameWork.MVVM;
using FrameWork.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using BLETool;
using EEGTool.Models;
using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
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
        private readonly Guid TargetServiceUuid = Guid.Parse("0003cdd0-0000-1000-8000-00805f9b0131");
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
        private const int MonitorTimerIntervalMilliseconds = 40;
        private const int SamplePumpIntervalMilliseconds = 10;
        private const int TargetPendingLatencyMilliseconds = 100;
        private const int MaxPendingLatencyMilliseconds = 500;

        public ICommand? BackHomeCommand { get; set; }
        public ICommand? ImpedanceCommand { get; set; }
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

            ImpedanceCommand = new RelayCommand(_ =>
            {
                IsShowMonitor = !IsShowMonitor;
            });

            
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

            var gatt = await _ble.GetGattProfileAsync();
            LogGattProfile(gatt);

            var targetService = gatt.FirstOrDefault(s => s.Uuid == TargetServiceUuid);
            if (targetService == null)
            {
                Logger.Debug($"[CollectionMonitorViewModel][GetGattProfile]:没有找到目标服务 {TargetServiceUuid}");
                return false;
            }
            
            var characteristics = targetService.Characteristics;
            var allCharacteristics = gatt.SelectMany(s => s.Characteristics).ToList();

            _writeCharacteristic = characteristics.FirstOrDefault(c => c.SupportsWrite)
                ?? allCharacteristics.FirstOrDefault(c => c.SupportsWrite);
            _notifyCharacteristic = characteristics.FirstOrDefault(c => c.SupportsNotify)
                ?? allCharacteristics.FirstOrDefault(c => c.SupportsNotify);

            if (_writeCharacteristic == null)
            {
                Logger.Debug("[CollectionMonitorViewModel][GetGattProfile]:没有找到写入数据特征~");
                return false;
            }

            if (_notifyCharacteristic == null)
            {
                Logger.Debug("[CollectionMonitorViewModel][GetGattProfile]:没有找到通知特征~");
                return false;
            }

            try
            {
                await _ble.SubscribeAsync(_notifyCharacteristic.ServiceUuid, _notifyCharacteristic.Uuid);
                _isNotifySubscribed = true;
                Logger.Info($"[CollectionMonitorViewModel][GetGattProfile]:Notify订阅成功 Service={_notifyCharacteristic.ServiceUuid}, Characteristic={_notifyCharacteristic.Uuid}");
            }
            catch (BleAccessDeniedException ex)
            {
                _isNotifySubscribed = false;
                Logger.Debug($"[CollectionMonitorViewModel][GetGattProfile]:Notify订阅访问被拒绝，继续尝试写入采集配置。Service={_notifyCharacteristic.ServiceUuid}, Characteristic={_notifyCharacteristic.Uuid}, Properties={_notifyCharacteristic.Properties}, Error={ex}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _isNotifySubscribed = false;
                Logger.Debug($"[CollectionMonitorViewModel][GetGattProfile]:Notify订阅无权限，继续尝试写入采集配置。Service={_notifyCharacteristic.ServiceUuid}, Characteristic={_notifyCharacteristic.Uuid}, Properties={_notifyCharacteristic.Properties}, Error={ex}");
            }

            Logger.Info($"[CollectionMonitorViewModel][GetGattProfile]:Write特征 Service={_writeCharacteristic.ServiceUuid}, Characteristic={_writeCharacteristic.Uuid}, Properties={_writeCharacteristic.Properties}");
            return true;
        }

        private static void LogGattProfile(IReadOnlyList<BleGattServiceInfo> gatt)
        {
            Logger.Info($"[CollectionMonitorViewModel][GetGattProfile]:GATT服务数量 {gatt.Count}");
            foreach (var service in gatt)
            {
                Logger.Info($"[CollectionMonitorViewModel][GetGattProfile]:Service={service.Uuid}, Characteristics={service.Characteristics.Count}");
                foreach (var characteristic in service.Characteristics)
                {
                    Logger.Info($"[CollectionMonitorViewModel][GetGattProfile]:  Characteristic={characteristic.Uuid}, Properties={characteristic.Properties}, SupportsWrite={characteristic.SupportsWrite}, SupportsNotify={characteristic.SupportsNotify}");
                }
            }
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
                _ = ReceivedCollectionData(result.DataFrame);
            }

        }

        private async Task HandleResponse(CommandResponse response)
        {
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
            int droppedSamples = 0;
            lock (_dataProcessingLock)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    _pendingSamples.Enqueue(samples[i]);
                }

                int maxPendingSamples = Math.Max(1,
                    _dataBuffer.SampleRate * MaxPendingLatencyMilliseconds / 1000);
                while (_pendingSamples.Count > maxPendingSamples)
                {
                    _pendingSamples.Dequeue();
                    droppedSamples++;
                }

                EnsureSamplePumpRunning();
            }

            if (droppedSamples > 0)
            {
                Logger.Info($"[CollectionMonitorViewModel][ReceivedCollectionData]:平滑队列超出实时延迟上限，丢弃旧样本 Count={droppedSamples}");
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
                new ZeroFftProcessor(),
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

        private sealed class ZeroFftProcessor : IFftProcessor
        {
            public float[] ComputeAmplitudeSpectrum(float[] timeData, int sampleRate)
            {
                if (timeData == null || timeData.Length == 0)
                {
                    return Array.Empty<float>();
                }

                return new float[(timeData.Length / 2) + 1];
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
