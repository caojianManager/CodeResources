using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace BLETool;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _executeAsync();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _executeAsync;
    private readonly Predicate<T?>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> executeAsync, Predicate<T?>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(ConvertParameter(parameter)) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _executeAsync(ConvertParameter(parameter));
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? ConvertParameter(object? parameter)
    {
        return parameter is T value ? value : default;
    }
}

public sealed class BleGattCharacteristicNode : INotifyPropertyChanged
{
    private bool _isSubscribed;

    public Guid ServiceUuid { get; init; }
    public Guid CharacteristicUuid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Uuid { get; init; } = string.Empty;
    public string Properties { get; init; } = string.Empty;
    public ushort Handle { get; init; }
    public bool SupportsRead { get; init; }
    public bool SupportsNotify { get; init; }
    public bool SupportsWrite { get; init; }

    public bool IsSubscribed
    {
        get => _isSubscribed;
        set
        {
            if (_isSubscribed == value)
            {
                return;
            }

            _isSubscribed = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSubscribed)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class BleGattServiceNode
{
    public string Name { get; init; } = string.Empty;
    public string Uuid { get; init; } = string.Empty;
    public ObservableCollection<BleGattCharacteristicNode> Characteristics { get; } = new();
}

public enum DeviceListMode
{
    Scanner,
    Bonded
}

public sealed class BleViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly BleManager _ble = new();
    private readonly ObservableCollection<BleGattServiceInfo> _gattProfileCache = new();

    private BleDeviceInfo? _selectedDevice;
    private BleGattCharacteristicNode? _selectedCharacteristic;
    private string _status = "Ready";
    private bool _isConnected;
    private string _writeData = "Hello BLE";
    private string _filterText = string.Empty;
    private DeviceListMode _deviceListMode = DeviceListMode.Scanner;
    private bool _showCharacteristics = true;
    private bool _showHex = true;
    private bool _notifyEnabled;
    private bool _syncingNotifyState;
    private long _receivedBytes;
    private string _readValueText = "0 Byte";
    private DateTime _notificationWindowStart = DateTime.UtcNow;

    public BleViewModel()
    {
        DevicesView = CollectionViewSource.GetDefaultView(Devices);
        DevicesView.Filter = FilterDevice;
        Devices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(DeviceCountText));

        _ble.ConfigureReconnect(enable: true, delayMs: 2000, maxAttempts: 5);
        _ble.DeviceDiscovered += OnDeviceDiscovered;
        _ble.DeviceUpdated += OnDeviceUpdated;
        _ble.ConnectionChanged += OnConnectionChanged;
        _ble.DataReceived += OnDataReceived;

        StartScanCommand = new AsyncRelayCommand(StartScanAsync);
        StopScanCommand = new AsyncRelayCommand(StopScanAsync);
        PairCommand = new AsyncRelayCommand(PairSelectedAsync, () => SelectedDevice != null);
        ConnectCommand = new AsyncRelayCommand(ConnectSelectedAsync, () => SelectedDevice != null);
        DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, () => IsConnected);
        ReconnectCommand = new AsyncRelayCommand(ReconnectAsync, () => !string.IsNullOrWhiteSpace(SelectedDevice?.DeviceId));
        WriteCommand = new AsyncRelayCommand(WriteAsync, () => IsConnected && SelectedCharacteristic?.SupportsWrite == true && !string.IsNullOrWhiteSpace(WriteData));
        PairDeviceCommand = new AsyncRelayCommand<BleDeviceInfo>(PairDeviceAsync, device => device != null);
        ConnectDeviceCommand = new AsyncRelayCommand<BleDeviceInfo>(ConnectDeviceAsync, device => device != null);
        ShowScannerCommand = new AsyncRelayCommand(() => SetDeviceModeAsync(DeviceListMode.Scanner));
        ShowBondedCommand = new AsyncRelayCommand(() => SetDeviceModeAsync(DeviceListMode.Bonded));
        RefreshServicesCommand = new AsyncRelayCommand(LoadGattProfileAsync, () => IsConnected);
        ReadSelectedCommand = new AsyncRelayCommand(ReadSelectedCharacteristicAsync, () => IsConnected && SelectedCharacteristic?.SupportsRead == true);
        ClearNotificationsCommand = new AsyncRelayCommand(ClearNotificationsAsync);

        RefreshCommandStates();
    }

    public ObservableCollection<BleDeviceInfo> Devices { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();
    public ObservableCollection<BleGattServiceNode> GattServices { get; } = new();
    public ObservableCollection<BleAdvertisementSection> AdvertisementRows { get; } = new();
    public ObservableCollection<string> NotificationMessages { get; } = new();

    public ICollectionView DevicesView { get; }

    public BleDeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (ReferenceEquals(_selectedDevice, value))
            {
                return;
            }

            _selectedDevice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDeviceName));
            OnPropertyChanged(nameof(SelectedDeviceAddress));
            RefreshAdvertisementRows();
            RefreshCommandStates();
        }
    }

    public BleGattCharacteristicNode? SelectedCharacteristic
    {
        get => _selectedCharacteristic;
        private set
        {
            if (ReferenceEquals(_selectedCharacteristic, value))
            {
                return;
            }

            _selectedCharacteristic = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCharacteristicName));
            OnPropertyChanged(nameof(SelectedCharacteristicUuid));
            OnPropertyChanged(nameof(SelectedCharacteristicProperties));
            OnPropertyChanged(nameof(SelectedCharacteristicHandleText));
            SyncSelectedCharacteristicState();
            RefreshCommandStates();
        }
    }

    public string SelectedDeviceName => SelectedDevice?.DisplayName ?? "No Device";

    public string SelectedDeviceAddress => SelectedDevice?.Address ?? "--";

    public string SelectedCharacteristicName => SelectedCharacteristic?.Name ?? "No Characteristic";

    public string SelectedCharacteristicUuid => SelectedCharacteristic?.Uuid ?? "--";

    public string SelectedCharacteristicProperties => SelectedCharacteristic?.Properties ?? "--";

    public string SelectedCharacteristicHandleText =>
        SelectedCharacteristic == null ? "--" : SelectedCharacteristic.Handle.ToString();

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetField(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionStateText));
                RefreshCommandStates();
            }
        }
    }

    public string ConnectionStateText => IsConnected ? "True" : "False";

    public string WriteData
    {
        get => _writeData;
        set
        {
            if (SetField(ref _writeData, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value))
            {
                DevicesView.Refresh();
                OnPropertyChanged(nameof(DeviceCountText));
            }
        }
    }

    public DeviceListMode DeviceListMode
    {
        get => _deviceListMode;
        set
        {
            if (SetField(ref _deviceListMode, value))
            {
                OnPropertyChanged(nameof(IsScannerMode));
                OnPropertyChanged(nameof(IsBondedMode));
                DevicesView.Refresh();
                OnPropertyChanged(nameof(DeviceCountText));
            }
        }
    }

    public bool IsScannerMode => DeviceListMode == DeviceListMode.Scanner;

    public bool IsBondedMode => DeviceListMode == DeviceListMode.Bonded;

    public string DeviceCountText => $"{DevicesView.Cast<object>().Count()} devices";

    public bool ShowCharacteristics
    {
        get => _showCharacteristics;
        set
        {
            if (SetField(ref _showCharacteristics, value))
            {
                RebuildGattServiceTree();
            }
        }
    }

    public bool ShowHex
    {
        get => _showHex;
        set => SetField(ref _showHex, value);
    }

    public bool NotifyEnabled
    {
        get => _notifyEnabled;
        set
        {
            if (!SetField(ref _notifyEnabled, value))
            {
                return;
            }

            if (!_syncingNotifyState)
            {
                _ = ToggleNotifyAsync(value);
            }
        }
    }

    public string ReadValueText
    {
        get => _readValueText;
        set => SetField(ref _readValueText, value);
    }

    public long ReceivedBytes
    {
        get => _receivedBytes;
        set
        {
            if (SetField(ref _receivedBytes, value))
            {
                OnPropertyChanged(nameof(ReceivedBytesText));
                OnPropertyChanged(nameof(ReceiveSpeedText));
            }
        }
    }

    public string ReceivedBytesText => $"{ReceivedBytes} Byte";

    public string ReceiveSpeedText
    {
        get
        {
            double elapsed = Math.Max((DateTime.UtcNow - _notificationWindowStart).TotalSeconds, 1);
            return $"{ReceivedBytes / elapsed:0.0} B/S";
        }
    }

    public ICommand StartScanCommand { get; }
    public ICommand StopScanCommand { get; }
    public ICommand PairCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand WriteCommand { get; }
    public ICommand PairDeviceCommand { get; }
    public ICommand ConnectDeviceCommand { get; }
    public ICommand ShowScannerCommand { get; }
    public ICommand ShowBondedCommand { get; }
    public ICommand RefreshServicesCommand { get; }
    public ICommand ReadSelectedCommand { get; }
    public ICommand ClearNotificationsCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose() => _ble.Dispose();

    public void SelectGattNode(object? node)
    {
        if (node is BleGattCharacteristicNode characteristicNode)
        {
            SelectedCharacteristic = characteristicNode;
        }
    }

    private Task StartScanAsync()
    {
        Devices.Clear();
        GattServices.Clear();
        AdvertisementRows.Clear();
        NotificationMessages.Clear();
        _gattProfileCache.Clear();
        SelectedDevice = null;
        SelectedCharacteristic = null;
        ReceivedBytes = 0;
        ReadValueText = "0 Byte";
        Status = "Scanning BLE devices...";
        _ble.StartScan();
        return Task.CompletedTask;
    }

    private Task StopScanAsync()
    {
        _ble.StopScan();
        Status = $"Scan stopped, found {Devices.Count} devices";
        return Task.CompletedTask;
    }

    private Task SetDeviceModeAsync(DeviceListMode mode)
    {
        DeviceListMode = mode;
        return Task.CompletedTask;
    }

    private Task PairSelectedAsync() => PairDeviceAsync(SelectedDevice);

    private Task ConnectSelectedAsync() => ConnectDeviceAsync(SelectedDevice);

    private async Task PairDeviceAsync(BleDeviceInfo? device)
    {
        if (device == null)
        {
            return;
        }

        SelectedDevice = device;
        try
        {
            Status = $"Pairing {device.DisplayName}...";
            bool ok = await _ble.PairAsync(device.DeviceId);
            if (ok)
            {
                device.IsPaired = true;
            }

            Status = ok ? "Pair success" : "Pair failed";
            AddLog(Status);
            DevicesView.Refresh();
        }
        catch (Exception ex)
        {
            HandleError("Pair", ex);
        }
    }

    private async Task ConnectDeviceAsync(BleDeviceInfo? device)
    {
        if (device == null)
        {
            return;
        }

        SelectedDevice = device;
        try
        {
            Status = $"Connecting {device.DisplayName}...";
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await _ble.ConnectAsync(device.DeviceId, cancellation.Token);
            await LoadGattProfileAsync();
        }
        catch (Exception ex)
        {
            HandleError("Connect", ex);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _ble.DisconnectAsync();
            GattServices.Clear();
            _gattProfileCache.Clear();
            SelectedCharacteristic = null;
            AddLog("Disconnected");
        }
        catch (Exception ex)
        {
            HandleError("Disconnect", ex);
        }
    }

    private async Task ReconnectAsync()
    {
        try
        {
            Status = "Reconnecting...";
            await _ble.ReconnectAsync();
            await LoadGattProfileAsync();
        }
        catch (Exception ex)
        {
            HandleError("Reconnect", ex);
        }
    }

    private async Task WriteAsync()
    {
        if (SelectedCharacteristic == null || string.IsNullOrWhiteSpace(WriteData))
        {
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(WriteData);
            await _ble.WriteAsync(SelectedCharacteristic.ServiceUuid, SelectedCharacteristic.CharacteristicUuid, data);
            AddLog($"[INFO] Write {SelectedCharacteristic.Uuid}: {BitConverter.ToString(data)}");
        }
        catch (Exception ex)
        {
            HandleError("Write", ex);
        }
    }

    private async Task LoadGattProfileAsync()
    {
        try
        {
            IReadOnlyList<BleGattServiceInfo> services = await _ble.GetGattProfileAsync();
            RunOnUI(() =>
            {
                _gattProfileCache.Clear();
                foreach (BleGattServiceInfo service in services)
                {
                    _gattProfileCache.Add(service);
                }

                RebuildGattServiceTree();
                Status = $"Services loaded: {_gattProfileCache.Count}";
            });
        }
        catch (Exception ex)
        {
            HandleError("Load services", ex);
        }
    }

    private async Task ReadSelectedCharacteristicAsync()
    {
        if (SelectedCharacteristic == null)
        {
            return;
        }

        try
        {
            byte[] data = await _ble.ReadAsync(SelectedCharacteristic.ServiceUuid, SelectedCharacteristic.CharacteristicUuid);
            ReadValueText = FormatPayload(data);
            AddLog($"[INFO] Read {SelectedCharacteristic.Uuid} success");
        }
        catch (Exception ex)
        {
            HandleError("Read", ex);
        }
    }

    private Task ClearNotificationsAsync()
    {
        NotificationMessages.Clear();
        ReceivedBytes = 0;
        ReadValueText = "0 Byte";
        _notificationWindowStart = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    private async Task ToggleNotifyAsync(bool enable)
    {
        if (SelectedCharacteristic == null)
        {
            return;
        }

        if (!SelectedCharacteristic.SupportsNotify)
        {
            SyncNotifyFlag(false);
            return;
        }

        try
        {
            if (enable)
            {
                await _ble.SubscribeAsync(SelectedCharacteristic.ServiceUuid, SelectedCharacteristic.CharacteristicUuid);
                SelectedCharacteristic.IsSubscribed = true;
                AddLog($"[INFO] Notify enabled: {SelectedCharacteristic.Uuid}");
            }
            else
            {
                await _ble.UnsubscribeAsync(SelectedCharacteristic.ServiceUuid, SelectedCharacteristic.CharacteristicUuid);
                SelectedCharacteristic.IsSubscribed = false;
                AddLog($"[INFO] Notify disabled: {SelectedCharacteristic.Uuid}");
            }
        }
        catch (Exception ex)
        {
            SelectedCharacteristic.IsSubscribed = false;
            SyncNotifyFlag(false);
            HandleError("Notify", ex);
        }
    }

    private void RebuildGattServiceTree()
    {
        string? previousUuid = SelectedCharacteristic?.Uuid;
        GattServices.Clear();

        foreach (BleGattServiceInfo service in _gattProfileCache)
        {
            var node = new BleGattServiceNode
            {
                Name = service.Name,
                Uuid = service.Uuid.ToString().ToUpperInvariant()
            };

            if (ShowCharacteristics)
            {
                foreach (BleGattCharacteristicInfo characteristic in service.Characteristics)
                {
                    node.Characteristics.Add(new BleGattCharacteristicNode
                    {
                        Name = characteristic.Name,
                        ServiceUuid = characteristic.ServiceUuid,
                        CharacteristicUuid = characteristic.Uuid,
                        Uuid = characteristic.Uuid.ToString().ToUpperInvariant(),
                        Properties = characteristic.Properties,
                        Handle = characteristic.Handle,
                        SupportsRead = characteristic.SupportsRead,
                        SupportsNotify = characteristic.SupportsNotify,
                        SupportsWrite = characteristic.SupportsWrite
                    });
                }
            }

            GattServices.Add(node);
        }

        BleGattCharacteristicNode? selectedNode =
            GattServices.SelectMany(service => service.Characteristics).FirstOrDefault(item => item.Uuid == previousUuid)
            ?? GattServices.SelectMany(service => service.Characteristics).FirstOrDefault(item => item.SupportsNotify)
            ?? GattServices.SelectMany(service => service.Characteristics).FirstOrDefault();

        SelectedCharacteristic = selectedNode;
    }

    private void OnDeviceDiscovered(object? sender, BleDeviceInfo device)
    {
        RunOnUI(() =>
        {
            UpsertDevice(device, autoSelectIfNeeded: true);
            AddLog($"[INFO] Device found: {device.DisplayName} {device.Address}");
        });
    }

    private void OnDeviceUpdated(object? sender, BleDeviceInfo device)
    {
        RunOnUI(() => UpsertDevice(device));
    }

    private void UpsertDevice(BleDeviceInfo snapshot, bool autoSelectIfNeeded = false)
    {
        BleDeviceInfo? existing = Devices.FirstOrDefault(device => device.DeviceId == snapshot.DeviceId);
        if (existing == null)
        {
            BleDeviceInfo clone = snapshot.Clone();
            Devices.Add(clone);
            if (SelectedDevice == null && autoSelectIfNeeded)
            {
                SelectedDevice = clone;
            }
        }
        else
        {
            existing.UpdateFrom(snapshot);
            if (SelectedDevice?.DeviceId == existing.DeviceId)
            {
                RefreshAdvertisementRows();
            }
        }

        DevicesView.Refresh();
        OnPropertyChanged(nameof(DeviceCountText));
    }

    private void OnConnectionChanged(object? sender, BleConnectionChangedEventArgs e)
    {
        RunOnUI(() =>
        {
            IsConnected = e.IsConnected;
            Status = e.IsConnected ? $"Connected: {e.DeviceId}" : $"Disconnected: {e.Reason}";
            AddLog($"[INFO] {Status}");
            if (!e.IsConnected)
            {
                GattServices.Clear();
                _gattProfileCache.Clear();
                SelectedCharacteristic = null;
                SyncNotifyFlag(false);
            }
        });
    }

    private void OnDataReceived(object? sender, BleDataReceivedEventArgs e)
    {
        RunOnUI(() =>
        {
            if (SelectedCharacteristic != null &&
                SelectedCharacteristic.ServiceUuid == e.ServiceUuid &&
                SelectedCharacteristic.CharacteristicUuid == e.CharacteristicUuid)
            {
                string payload = FormatPayload(e.Data);
                NotificationMessages.Add($"[{e.Timestamp:HH:mm:ss.fff}] {payload}");
                if (NotificationMessages.Count > 300)
                {
                    NotificationMessages.RemoveAt(0);
                }

                ReceivedBytes += e.Data.Length;
                ReadValueText = payload;
            }

            AddLog($"[INFO] Notify {e.CharacteristicUuid}: {BitConverter.ToString(e.Data)}");
        });
    }

    private void RefreshAdvertisementRows()
    {
        AdvertisementRows.Clear();
        if (SelectedDevice == null)
        {
            return;
        }

        foreach (BleAdvertisementSection row in SelectedDevice.AdvertisementSections)
        {
            AdvertisementRows.Add(new BleAdvertisementSection
            {
                Length = row.Length,
                AdType = row.AdType,
                Data = row.Data
            });
        }
    }

    private bool FilterDevice(object item)
    {
        if (item is not BleDeviceInfo device)
        {
            return false;
        }

        if (DeviceListMode == DeviceListMode.Bonded && !device.IsPaired)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            return true;
        }

        string query = FilterText.Trim();
        return device.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
               || device.Address.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private string FormatPayload(byte[] data)
    {
        if (ShowHex)
        {
            return BitConverter.ToString(data);
        }

        try
        {
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return BitConverter.ToString(data);
        }
    }

    private void SyncSelectedCharacteristicState()
    {
        NotificationMessages.Clear();
        ReceivedBytes = 0;
        _notificationWindowStart = DateTime.UtcNow;
        ReadValueText = "0 Byte";
        SyncNotifyFlag(SelectedCharacteristic?.IsSubscribed == true);
    }

    private void SyncNotifyFlag(bool value)
    {
        _syncingNotifyState = true;
        NotifyEnabled = value;
        _syncingNotifyState = false;
    }

    private void HandleError(string action, Exception ex)
    {
        string message = $"{action} failed: {ex.Message}";
        RunOnUI(() =>
        {
            Status = message;
            AddLog($"[WARN] {message}");
        });
    }

    private void AddLog(string message)
    {
        if (Logs.Count > 500)
        {
            Logs.RemoveAt(0);
        }

        Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void RefreshCommandStates()
    {
        RaiseCanExecuteChanged(StartScanCommand);
        RaiseCanExecuteChanged(StopScanCommand);
        RaiseCanExecuteChanged(PairCommand);
        RaiseCanExecuteChanged(ConnectCommand);
        RaiseCanExecuteChanged(DisconnectCommand);
        RaiseCanExecuteChanged(ReconnectCommand);
        RaiseCanExecuteChanged(WriteCommand);
        RaiseCanExecuteChanged(PairDeviceCommand);
        RaiseCanExecuteChanged(ConnectDeviceCommand);
        RaiseCanExecuteChanged(RefreshServicesCommand);
        RaiseCanExecuteChanged(ReadSelectedCommand);
        RaiseCanExecuteChanged(ClearNotificationsCommand);
    }

    private static void RaiseCanExecuteChanged(ICommand command)
    {
        switch (command)
        {
            case AsyncRelayCommand asyncRelayCommand:
                asyncRelayCommand.RaiseCanExecuteChanged();
                break;
            case AsyncRelayCommand<BleDeviceInfo> asyncRelayCommandWithParameter:
                asyncRelayCommandWithParameter.RaiseCanExecuteChanged();
                break;
        }
    }

    private static void RunOnUI(Action action)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            action();
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(action);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
