using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Event;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
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
            var results = _commandStreamParser.Push(e.Data);
            foreach (var result in results)
            {
                HandleCommandResult(result);
            }
        }

        /// <summary>
        /// 开始监测
        /// </summary>
        private void StartMonitor()
        {
            //1.获取采集配置信息
            var cInfo = CollectionInfoManager.GetInstance().Info;

            //2.配置采集指令-并发送给下位机(MCU);
            var channelList = TemplateFileManager.GetInstance()
                .GetCurrentChannelList(cInfo.Template)
                .Where(channel => channel >= 1 && channel <= CommandManager.ChannelCount)
                .Distinct()
                .ToList();

            if (channelList.Count == 0)
            {
                Logger.Info("[CollectionMonitorViewModel][StartMonitor]:当前模板没有有效通道，默认开启16通道采集");
                channelList = Enumerable.Range(1, CommandManager.ChannelCount).ToList();
            }
            ushort channelMask = CommandManager.BuildChannelMask(channelList);
            ushort sampleRate = cInfo.SampleRate > 0 ? (ushort)cInfo.SampleRate : (ushort)250;
            ushort durationSeconds = cInfo.Template.Time > 0 ? (ushort)cInfo.Template.Time : (ushort)60;

            byte[] command = CommandManager.BuildConfigureCollectionCommand(
                channelMask,
                sampleRate,
                durationSeconds);
            Logger.Info($"[CollectionMonitorViewModel][StartMonitor]:采集配置指令 {CommandManager.ToHexString(command)}");
            _=WriteDataToBLE(command);
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
            GetGattProfile();
            StartMonitor();
        }

        private async Task GetGattProfile()
        {
            if (_ble == null ||(_writeCharacteristic != null && _notifyCharacteristic != null))
                return;
            var gatt = await _ble.GetGattProfileAsync();
            var targetService = gatt.FirstOrDefault(s => s.Uuid == TargetServiceUuid);
            if (targetService == null)
            {
                // 没找到目标服务
                return;
            }
            
            var characteristics = targetService.Characteristics;

            _writeCharacteristic = characteristics.FirstOrDefault(c => c.SupportsWrite);
            _notifyCharacteristic = characteristics.FirstOrDefault(c => c.SupportsNotify);

            if (_writeCharacteristic == null)
            {
                Logger.Debug("[CollectionMonitorViewModel][GetGattProfile]:没有找到写入数据特征~");
                return;
            }

            if (_notifyCharacteristic == null)
            {
                Logger.Debug("[CollectionMonitorViewModel][GetGattProfile]:没有找到通知特征~");
                return;
            }

            _ble.SubscribeAsync(_notifyCharacteristic.ServiceUuid, _notifyCharacteristic.Uuid);
        }

        private async Task WriteDataToBLE(byte[] data)
        {
            if(_ble == null || _writeCharacteristic == null)
                return;
            await _ble.WriteAsync(_writeCharacteristic.ServiceUuid, _writeCharacteristic.Uuid, data);
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
                return;
            }

            if (result.Battery != null)
            {
                Logger.Info($"[CollectionMonitorViewModel][DataReceived]:收到电量 {result.Battery.ElectricityQuantity}");
                return;
            }

            if (result.DataFrame != null)
            {
                Logger.Debug($"[CollectionMonitorViewModel][DataReceived]:收到数据帧 {result.DataFrame.CommandType}, Channels={result.DataFrame.ChannelCount}, Samples={result.DataFrame.SampleCount}");
            }
        }

    }
}
