using BLETool;
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
                var template = TemplateFileManager.GetInstance().AllTemplates
                    .Find(o => o.Name.Equals(SelectedTemplate));

                CollectionInfoManager.GetInstance().UpdateInfo(new CollectionInfo()
                {
                    IsCaptureVideo = IsVideoRecordYes,
                    SampleRate = int.Parse(SelectedSampleRate.Replace("Hz","")),
                    Template = template ?? new TemplateModel()
                });

                EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(CollectionMonitorViewModel));
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
