using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Media.Audio;
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


        public DeviceConnectViewModel()
        {
            Config();
        }

        private void Config()
        {
            _ble = BleToolKit.Shared;
            _ble.DeviceDiscovered += (_, d) => { DeviceDiscovered(d); };
            _ble.DeviceUpdated += (_, d) => { DeviceUpdated(d); };

            ScanDeviceCommand = new RelayCommand((o) =>
            {
                _ = ScanBTDevice();
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


        /// <summary>
        /// 扫描蓝牙设备
        /// </summary>
        /// <returns></returns>
        private async Task ScanBTDevice()
        {
            if(_ble == null)
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
                await Task.Delay(10);
                BtDevices.Add(dv);
            }
        }
    }
}
