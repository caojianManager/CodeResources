using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BLETool;
using EEGTool.Models.Template;
using FrameWork.MVVM;
using Framework.MVVM.Commands;

namespace EEGTool.ViewModels.Collection
{
    public class CollectionConfigViewModel : BindableBase
    {
        private readonly BleManager _ble;

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
            _ble.ConnectionChanged += OnConnectionChanged;

            CollectionCommand = new RelayCommand(_ => { });
            LoadTemplateItems();
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

        private void OnConnectionChanged(object? sender, BleConnectionChangedEventArgs e)
        {
            RunOnUI(SyncCurrentConnectedDeviceInfo);
        }

        private void SyncCurrentConnectedDeviceInfo()
        {
            var connectedDevice = _ble.GetDiscoveredDevices().FirstOrDefault(d => d.IsConnected);
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
