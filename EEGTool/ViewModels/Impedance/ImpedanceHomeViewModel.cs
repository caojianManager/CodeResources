using BLETool;
using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
using EEGTool.Models.Impedance;
using Framework.Event;
using FrameWork.Event;
using FrameWork.Log;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        private const int ConfigureImpedanceTimeoutMilliseconds = 3000;

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
            EventUtilManager.EventUitl.OnEvent<DataFrame>(EventName.RECEVIED_IMPEDANCE_DATA, dataFrame);
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
