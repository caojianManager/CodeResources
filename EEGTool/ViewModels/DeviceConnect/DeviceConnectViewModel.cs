using System;
using System.Collections.ObjectModel;
using System.Linq;
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
            _ble.DeviceDiscovered += (_, d) => { DeviceDiscovered(d); };
            _ble.DeviceUpdated += (_, d) => { DeviceUpdated(d); };
            _ble.ConnectionChanged += (_, e) =>
            {
                RunOnUI(() =>
                {
                    IsConnecting = false;
                    var device = BtDevices.FirstOrDefault(x => x.DeviceId == e.DeviceId);
                    if (device != null)
                    {
                        device.IsConnected = e.IsConnected;
                    }
                });
            };

            ScanDeviceCommand = new RelayCommand((o) =>
            {
                _ = ScanBTDevice();
            });

            ToggleDeviceConnectionCommand = new RelayCommand((o) =>
            {
                _ = ToggleDeviceConnectionAsync(o as BleDeviceInfo);
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
            if (_ble == null || device == null || string.IsNullOrWhiteSpace(device.DeviceId) || IsConnecting)
            {
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
                        MessageBox.Show("设备已断开连接。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                else
                {
                    IsConnecting = true;
                    ConnectingDeviceId = device.DeviceId;
                    RunOnUI(() =>
                    {
                        foreach (var dv in BtDevices)
                        {
                            dv.IsConnecting = dv.DeviceId == device.DeviceId;
                        }
                    });

                    await _ble.ConnectAsync(device.DeviceId);

                    RunOnUI(() =>
                    {
                        foreach (var dv in BtDevices)
                        {
                            dv.IsConnected = dv.DeviceId == device.DeviceId;
                        }

                        MessageBox.Show("蓝牙连接成功。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                RunOnUI(() =>
                {
                    MessageBox.Show($"蓝牙连接失败：{ex.Message}", "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            finally
            {
                if (isConnectAction)
                {
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
            await Task.Delay(500);
            _ble.StopScan();


            //获取扫描结果
            var devices = _ble.GetDiscoveredDevices();

            //按照蓝牙名称过滤设备
            devices = devices.Where(d => d.Name.StartsWith("16CH_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            BtDevices.Clear();

            foreach (var dv in devices)
            {
                await Task.Delay(100);
                BtDevices.Add(dv);
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
