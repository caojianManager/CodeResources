using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BLETool;
using FrameWork.MVVM;
using Framework.MVVM.Commands;

namespace EEGTool.ViewModels.DeviceConnect
{
    public class DeviceConnectViewModel : BindableBase
    {

        private BleManager _ble;
        private readonly SemaphoreSlim _connectGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _lifetimeCts = new CancellationTokenSource();
        private bool _isViewUnloading;
        private int _activeConnectOperations;
        private ObservableCollection<BleDeviceInfo> _btDevices = new ObservableCollection<BleDeviceInfo>();
        public ObservableCollection<BleDeviceInfo> BtDevices
        {
            get => _btDevices;
            set => SetProperty(ref _btDevices, value);
        }

        private BleDeviceInfo _selectedBtDevice = null;

        public BleDeviceInfo SelectedBtDevice
        {
            get => _selectedBtDevice;
            set => SetProperty(ref _selectedBtDevice, value);
        }

        public ICommand? ScanDeviceCommand { get; set; }
        public ICommand? ToggleDeviceConnectionCommand { get; set; }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                if (SetProperty(ref _isConnecting, value))
                {
                    OnPropertyChanged(nameof(CanScan));
                    OnPropertyChanged(nameof(HasConnectingDevice));
                }
            }
        }

        private string _connectingDeviceId = string.Empty;
        public string ConnectingDeviceId
        {
            get => _connectingDeviceId;
            set
            {
                if (SetProperty(ref _connectingDeviceId, value))
                {
                    OnPropertyChanged(nameof(HasConnectingDevice));
                }
            }
        }

        public bool CanScan => !IsConnecting;
        public bool HasConnectingDevice => IsConnecting && !string.IsNullOrWhiteSpace(ConnectingDeviceId);


        public DeviceConnectViewModel()
        {
            Config();
        }

        private void Config()
        {
            _ble = BleToolKit.Shared;
            _ble.DeviceDiscovered += OnDeviceDiscovered;
            _ble.DeviceUpdated += OnDeviceUpdated;
            _ble.ConnectionChanged += OnConnectionChanged;

            ScanDeviceCommand = new RelayCommand((o) =>
            {
                _ = ScanBTDevice();
            });

            ToggleDeviceConnectionCommand = new RelayCommand((o) =>
            {
                _ = ToggleDeviceConnectionAsync(o as BleDeviceInfo);
            });
        }

        private void OnDeviceDiscovered(object? sender, BleDeviceInfo deviceInfo)
        {
            DeviceDiscovered(deviceInfo);
        }

        private void OnDeviceUpdated(object? sender, BleDeviceInfo deviceInfo)
        {
            DeviceUpdated(deviceInfo);
        }

        private void OnConnectionChanged(object? sender, BleConnectionChangedEventArgs e)
        {
            RunOnUI(() =>
            {
                var device = BtDevices.FirstOrDefault(x => x.DeviceId == e.DeviceId);
                if (device != null)
                {
                    device.IsConnected = e.IsConnected;
                }
            });
        }

        private void DeviceDiscovered(BleDeviceInfo deviceInfo)
        {
            Console.WriteLine($"{deviceInfo.DisplayName} {deviceInfo.Address} {deviceInfo.Rssi}");
        }

        private void DeviceUpdated(BleDeviceInfo deviceInfo)
        {
            Console.WriteLine($"更新: {deviceInfo.DisplayName} {deviceInfo.Rssi}");
        }

        private async Task ToggleDeviceConnectionAsync(BleDeviceInfo? device)
        {
            if (_ble == null || device == null || string.IsNullOrWhiteSpace(device.DeviceId))
            {
                return;
            }

            if (!await _connectGate.WaitAsync(0))
            {
                RunOnUI(() =>
                {
                    MessageBox.Show("当前已有连接任务正在进行，请稍候。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
                return;
            }

            bool isConnectAction = !device.IsConnected;
            try
            {
                if (device.IsConnected)
                {
                    await _ble.DisconnectAsync();
                    RunOnUI(() =>
                    {
                        device.IsConnected = false;
                        if (!_isViewUnloading)
                        {
                            MessageBox.Show("设备已断开连接。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    });
                }
                else
                {
                    Interlocked.Increment(ref _activeConnectOperations);
                    IsConnecting = true;
                    ConnectingDeviceId = device.DeviceId;
                    RunOnUI(() =>
                    {
                        foreach (var dv in BtDevices)
                        {
                            dv.IsConnecting = dv.DeviceId == device.DeviceId;
                        }
                    });

                    await _ble.ConnectAsync(device.DeviceId, _lifetimeCts.Token);

                    RunOnUI(() =>
                    {
                        foreach (var dv in BtDevices)
                        {
                            dv.IsConnected = dv.DeviceId == device.DeviceId;
                        }

                        if (!_isViewUnloading)
                        {
                            MessageBox.Show("蓝牙连接成功。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                if (!_isViewUnloading)
                {
                    RunOnUI(() =>
                    {
                        MessageBox.Show("连接任务已取消，请重试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                if (!_isViewUnloading)
                {
                    RunOnUI(() =>
                    {
                        MessageBox.Show($"蓝牙连接失败：{ex.Message}", "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            }
            finally
            {
                if (isConnectAction)
                {
                    var remaining = Interlocked.Decrement(ref _activeConnectOperations);
                    if (remaining <= 0)
                    {
                        Interlocked.Exchange(ref _activeConnectOperations, 0);
                        IsConnecting = false;
                        ConnectingDeviceId = string.Empty;
                        RunOnUI(() =>
                        {
                            foreach (var dv in BtDevices)
                            {
                                dv.IsConnecting = false;
                            }
                        });
                    }
                }

                _connectGate.Release();
            }
        }

        /// <summary>
        /// 扫描蓝牙设备
        /// </summary>
        /// <returns></returns>
        private async Task ScanBTDevice()
        {
            if(_ble == null)
                return;
            if (IsConnecting)
                return;
            _ble.StartScan();
            await Task.Delay(500, _lifetimeCts.Token);
            _ble.StopScan();


            //获取扫描结果
            var devices = _ble.GetDiscoveredDevices();

            //按照蓝牙名称过滤设备
            devices = devices.Where(d => d.Name.StartsWith("16CH_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            BtDevices.Clear();

            foreach (var dv in devices)
            {
                await Task.Delay(100, _lifetimeCts.Token);
                BtDevices.Add(dv);
            }
        }

        public async Task OnViewUnloadedAsync()
        {
            if (_isViewUnloading)
            {
                return;
            }

            _isViewUnloading = true;

            try
            {
                _lifetimeCts.Cancel();
                _ble.StopScan();
                await _ble.DisconnectAsync();
            }
            catch
            {
                // Ignore cleanup failures during page unload.
            }
            finally
            {
                _ble.DeviceDiscovered -= OnDeviceDiscovered;
                _ble.DeviceUpdated -= OnDeviceUpdated;
                _ble.ConnectionChanged -= OnConnectionChanged;
                Interlocked.Exchange(ref _activeConnectOperations, 0);

                IsConnecting = false;
                ConnectingDeviceId = string.Empty;
                foreach (var dv in BtDevices)
                {
                    dv.IsConnecting = false;
                }

                _lifetimeCts.Dispose();
                _lifetimeCts = new CancellationTokenSource();
                _isViewUnloading = false;
            }
        }

        private static void RunOnUI(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }
    }
}
