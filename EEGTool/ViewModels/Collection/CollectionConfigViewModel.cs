using BLETool;
using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
using EEGTool.Models.Template;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Common;
using FrameWork.Event;
using FrameWork.MVVM;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EEGTool.ViewModels.Collection
{
    public class CollectionConfigViewModel : BindableBase
    {
        private readonly BleManager _ble;
        private bool _isLoadingPreferences = true;
        private bool _isSubscribedToBleEvents;
        private bool _isStartingCollection;

        public ObservableCollection<string> SampleRateItems { get; } = new()
        {
            "500Hz",
            "250Hz"
        };

        private string _selectedSampleRate = "250Hz";
        public string SelectedSampleRate
        {
            get => _selectedSampleRate;
            set
            {
                if (SetProperty(ref _selectedSampleRate, value))
                {
                    SaveCollectionPreferences();
                    OnPropertyChanged(nameof(CanCollection));
                }
            }
        }

        public ObservableCollection<string> TemplateItems { get; } = new();

        private string? _selectedTemplate;
        public string? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    SaveCollectionPreferences();
                    OnPropertyChanged(nameof(CanCollection));
                }
            }
        }

        private bool _isVideoRecordYes;
        public bool IsVideoRecordYes
        {
            get => _isVideoRecordYes;
            set
            {
                if (!SetProperty(ref _isVideoRecordYes, value))
                {
                    return;
                }

                if (value)
                {
                    IsVideoRecordNo = false;
                }

                SaveCollectionPreferences();
                OnPropertyChanged(nameof(CanCollection));
            }
        }

        private bool _isVideoRecordNo = true;
        public bool IsVideoRecordNo
        {
            get => _isVideoRecordNo;
            set
            {
                if (!SetProperty(ref _isVideoRecordNo, value))
                {
                    return;
                }

                if (value)
                {
                    IsVideoRecordYes = false;
                }

                SaveCollectionPreferences();
                OnPropertyChanged(nameof(CanCollection));
            }
        }

        private string _currentDeviceName = "--";
        public string CurrentDeviceName
        {
            get => _currentDeviceName;
            set => SetProperty(ref _currentDeviceName, value);
        }

        private string _currentConnectionStatus = "未连接";
        public string CurrentConnectionStatus
        {
            get => _currentConnectionStatus;
            set => SetProperty(ref _currentConnectionStatus, value);
        }

        private Brush _currentConnectionStatusBrush = Brushes.Red;
        public Brush CurrentConnectionStatusBrush
        {
            get => _currentConnectionStatusBrush;
            set => SetProperty(ref _currentConnectionStatusBrush, value);
        }

        public bool CanCollection =>
            IsDeviceConnected &&
            !_isStartingCollection &&
            !string.IsNullOrWhiteSpace(SelectedSampleRate) &&
            !string.IsNullOrWhiteSpace(SelectedTemplate) &&
            (IsVideoRecordYes || IsVideoRecordNo);

        public ICommand? CollectionCommand { get; }

        private bool IsDeviceConnected => CurrentConnectionStatus == "已连接";

        public CollectionConfigViewModel()
        {
            _ble = BleToolKit.Shared;
            SubscribeBleEvents();

            CollectionCommand = new RelayCommand(_ =>
            {
                _ = StartCollectionAsync();
            });
            LoadTemplateItems();
            LoadCollectionPreferences();
            _isLoadingPreferences = false;
            SyncCurrentConnectedDeviceInfo();
        }

        private void LoadTemplateItems()
        {
            TemplateItems.Clear();
            TemplateFileManager.GetInstance().ReadAllTemplates();

            foreach (var template in TemplateFileManager.GetInstance().AllTemplates)
            {
                TemplateItems.Add(string.IsNullOrWhiteSpace(template.Name)
                    ? template.TemplateId
                    : template.Name);
            }

            SelectedTemplate = TemplateItems.FirstOrDefault();
        }

        private void LoadCollectionPreferences()
        {
            var config = Config.Instance;

            SelectedSampleRate = SampleRateItems.Contains(config.CollectionSelectedSampleRate)
                ? config.CollectionSelectedSampleRate
                : SampleRateItems.FirstOrDefault() ?? string.Empty;

            SelectedTemplate = TemplateItems.Contains(config.CollectionSelectedTemplate)
                ? config.CollectionSelectedTemplate
                : TemplateItems.FirstOrDefault();

            IsVideoRecordYes = config.CollectionIsVideoRecordYes;
            IsVideoRecordNo = !config.CollectionIsVideoRecordYes;
        }

        private void SaveCollectionPreferences()
        {
            if (_isLoadingPreferences)
            {
                return;
            }

            var config = Config.Instance;
            config.CollectionSelectedSampleRate = SelectedSampleRate;
            config.CollectionSelectedTemplate = SelectedTemplate ?? string.Empty;
            config.CollectionIsVideoRecordYes = IsVideoRecordYes;
            config.Save();
        }

        private async Task StartCollectionAsync()
        {
            if (_isStartingCollection)
            {
                return;
            }

            _isStartingCollection = true;
            OnPropertyChanged(nameof(CanCollection));

            try
            {
                var template = TemplateFileManager.GetInstance().AllTemplates
                    .Find(o => o.Name.Equals(SelectedTemplate));

                var collectionInfo = new CollectionInfo()
                {
                    IsCaptureVideo = IsVideoRecordYes,
                    SampleRate = int.Parse(SelectedSampleRate.Replace("Hz","")),
                    Template = template ?? new TemplateModel()
                };

                if (!await VerifyBleWriteAccessForCollectionAsync())
                {
                    return;
                }

                CollectionInfoManager.GetInstance().UpdateInfo(collectionInfo);

                EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(CollectionMonitorViewModel));
            }
            finally
            {
                _isStartingCollection = false;
                OnPropertyChanged(nameof(CanCollection));
            }
        }

        private async Task<bool> VerifyBleWriteAccessForCollectionAsync()
        {
            if (_ble.GetCurrentConnectedDevice() == null)
            {
                MessageBox.Show("请先连接蓝牙设备。", "蓝牙未连接", MessageBoxButton.OK, MessageBoxImage.Information);
                SyncCurrentConnectedDeviceInfo();
                return false;
            }

            IReadOnlyList<BleGattServiceInfo> gatt;
            try
            {
                gatt = await _ble.GetGattProfileAsync();
            }
            catch (BleAccessDeniedException)
            {
                ShowBleHeldByOtherSoftwareMessage();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                ShowBleHeldByOtherSoftwareMessage();
                return false;
            }
            catch (BleException ex)
            {
                MessageBox.Show(ex.Message, "蓝牙连接异常", MessageBoxButton.OK, MessageBoxImage.Warning);
                SyncCurrentConnectedDeviceInfo();
                return false;
            }

            var writeCharacteristic = BleGattProfileHelper.FindWriteCharacteristic(gatt);

            if (writeCharacteristic == null)
            {
                MessageBox.Show(
                    "当前蓝牙设备已连接，但没有找到可写入的数据特征。请确认连接的是采集设备，并尝试断开后在本软件内重新连接。",
                    "蓝牙不可写入",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            byte[] command = EEGTool.Models.BLE.CommandManager.BuildQueryBatteryCommand();

            try
            {
                await _ble.WriteAsync(writeCharacteristic.ServiceUuid, writeCharacteristic.Uuid, command);
            }
            catch (BleAccessDeniedException)
            {
                ShowBleHeldByOtherSoftwareMessage();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                ShowBleHeldByOtherSoftwareMessage();
                return false;
            }
            catch (BleException ex) when (ex.Message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
            {
                ShowBleHeldByOtherSoftwareMessage();
                return false;
            }
            catch (BleException ex)
            {
                MessageBox.Show(ex.Message, "蓝牙写入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static void ShowBleHeldByOtherSoftwareMessage()
        {
            MessageBox.Show(
                "当前蓝牙设备已连接，但本软件没有读取或写入权限，可能被其他软件持有。\n\n请关闭第三方蓝牙工具，或在系统蓝牙中断开后，回到本软件的蓝牙连接页面重新连接。",
                "蓝牙设备被占用",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void OnConnectionChanged(object? sender, BleConnectionChangedEventArgs e)
        {
            RunOnUI(SyncCurrentConnectedDeviceInfo);
        }

        private void OnDeviceDiscoveredOrUpdated(object? sender, BleDeviceInfo e)
        {
            RunOnUI(SyncCurrentConnectedDeviceInfo);
        }

        public void OnViewLoaded()
        {
            SubscribeBleEvents();
            SyncCurrentConnectedDeviceInfo();
        }

        public void OnViewUnloaded()
        {
            UnsubscribeBleEvents();
        }

        private void SubscribeBleEvents()
        {
            if (_isSubscribedToBleEvents)
            {
                return;
            }

            _ble.DeviceDiscovered += OnDeviceDiscoveredOrUpdated;
            _ble.DeviceUpdated += OnDeviceDiscoveredOrUpdated;
            _ble.ConnectionChanged += OnConnectionChanged;
            _isSubscribedToBleEvents = true;
        }

        private void UnsubscribeBleEvents()
        {
            if (!_isSubscribedToBleEvents)
            {
                return;
            }

            _ble.DeviceDiscovered -= OnDeviceDiscoveredOrUpdated;
            _ble.DeviceUpdated -= OnDeviceDiscoveredOrUpdated;
            _ble.ConnectionChanged -= OnConnectionChanged;
            _isSubscribedToBleEvents = false;
        }

        private void SyncCurrentConnectedDeviceInfo()
        {
            var connectedDevice = _ble.GetCurrentConnectedDevice();
            if (connectedDevice == null)
            {
                CurrentDeviceName = "--";
                CurrentConnectionStatus = "未连接";
                CurrentConnectionStatusBrush = Brushes.Red;
                OnPropertyChanged(nameof(CanCollection));
                return;
            }

            CurrentDeviceName = string.IsNullOrWhiteSpace(connectedDevice.Name) ? "--" : connectedDevice.Name;
            CurrentConnectionStatus = "已连接";
            CurrentConnectionStatusBrush = Brushes.Green;
            OnPropertyChanged(nameof(CanCollection));
        }

        private static void RunOnUI(System.Action action)
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
