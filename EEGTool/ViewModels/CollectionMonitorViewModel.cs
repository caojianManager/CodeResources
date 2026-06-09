using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Event;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BLETool;
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

        public ICommand? BackHomeCommand { get; set; }
        public ICommand? StartRecordCommand { get; set; }

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

        }

        private void ClickPlaybackBtn()
        {

        }

        private void ClickCollectionBtn()
        {

        }

        public void OnHide()
        {

        }

        public void OnShow()
        {
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
                if (result.Response.CommandType == BleCommandType.ConfigureCollectionResponse)
                {
                    if (!result.Response.IsSuccess)
                    {
                        Logger.Debug($"[CollectionMonitorViewModel][DataReceived]:配置采集失败 Status={result.Response.StatusCode}, Detail={result.Response.ErrorDetail}");
                        return;
                    }

                    _ = ReceivedConfigCollection();
                }
            }

            if (result.Battery != null)
            {
                Logger.Info($"[CollectionMonitorViewModel][DataReceived]:收到电量 {result.Battery.ElectricityQuantity}");
            }

            if (result.DataFrame != null)
            {
                Logger.Debug($"[CollectionMonitorViewModel][DataReceived]:收到数据帧 {result.DataFrame.CommandType}, Channels={result.DataFrame.ChannelCount}, Samples={result.DataFrame.SampleCount}");
            }

        }

        private async Task ReceivedConfigCollection()
        {
            //收到配置采集成功的回复
            //1.开始采集指令-并发送给下位机(MCU);
            byte[] command = CommandManager.BuildStartCollectionCommand();
            Logger.Info($"[CollectionMonitorViewModel][ReceivedConfigCollection]:开始采集指令 {CommandManager.ToHexString(command)}");
            await WriteDataToBLE(command);

        }

    }
}
