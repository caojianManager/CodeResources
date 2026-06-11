using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;

namespace BLETool;

public sealed class BleAdvertisementSection
{
    public int Length { get; init; }
    public string AdType { get; init; } = string.Empty;
    public string Data { get; init; } = string.Empty;
}

public sealed class BleGattCharacteristicInfo
{
    public string Name { get; init; } = string.Empty;
    public Guid ServiceUuid { get; init; }
    public Guid Uuid { get; init; }
    public string Properties { get; init; } = string.Empty;
    public ushort Handle { get; init; }
    public bool SupportsRead { get; init; }
    public bool SupportsNotify { get; init; }
    public bool SupportsWrite { get; init; }
}
public sealed class BleGattServiceInfo
{
    public string Name { get; init; } = string.Empty;
    public Guid Uuid { get; init; }
    public IReadOnlyList<BleGattCharacteristicInfo> Characteristics { get; init; } =
        Array.Empty<BleGattCharacteristicInfo>();
}

public sealed class BleDeviceInfo : INotifyPropertyChanged
{
    private string _deviceId = string.Empty;
    private string _name = string.Empty;
    private string _address = string.Empty;
    private short _rssi;
    private bool _isPaired;
    private bool _isConnected;
    private bool _isConnectedByCurrentApp;
    private bool _hasConnectableAdvertisement;
    private bool _isConnectableAdvertisement;
    private bool _isConnecting;
    private string _advertisementType = "N/A";
    private IReadOnlyList<BleAdvertisementSection> _advertisementSections = Array.Empty<BleAdvertisementSection>();

    public string DeviceId
    {
        get => _deviceId;
        set => SetField(ref _deviceId, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Address
    {
        get => _address;
        set => SetField(ref _address, value);
    }

    public short Rssi
    {
        get => _rssi;
        set => SetField(ref _rssi, value);
    }

    public bool IsPaired
    {
        get => _isPaired;
        set => SetField(ref _isPaired, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetField(ref _isConnected, value))
            {
                OnConnectionOwnershipChanged();
            }
        }
    }

    public bool IsConnectedByCurrentApp
    {
        get => _isConnectedByCurrentApp;
        set
        {
            if (SetField(ref _isConnectedByCurrentApp, value))
            {
                OnConnectionOwnershipChanged();
            }
        }
    }

    public bool HasConnectableAdvertisement
    {
        get => _hasConnectableAdvertisement;
        set
        {
            if (SetField(ref _hasConnectableAdvertisement, value))
            {
                OnConnectionOwnershipChanged();
            }
        }
    }

    public bool IsConnectableAdvertisement
    {
        get => _isConnectableAdvertisement;
        set
        {
            if (SetField(ref _isConnectableAdvertisement, value))
            {
                OnConnectionOwnershipChanged();
            }
        }
    }

    public bool IsOccupiedByOther => !IsConnectedByCurrentApp
        && (IsConnected || (HasConnectableAdvertisement && !IsConnectableAdvertisement));

    public bool IsConnecting
    {
        get => _isConnecting;
        set => SetField(ref _isConnecting, value);
    }

    public string AdvertisementType
    {
        get => _advertisementType;
        set => SetField(ref _advertisementType, value);
    }

    public IReadOnlyList<BleAdvertisementSection> AdvertisementSections
    {
        get => _advertisementSections;
        set => SetField(ref _advertisementSections, value);
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "N/A" : Name;

    public string SignalText => $"{Rssi} dBm";

    public event PropertyChangedEventHandler? PropertyChanged;

    public BleDeviceInfo Clone()
    {
        return new BleDeviceInfo
        {
            DeviceId = DeviceId,
            Name = Name,
            Address = Address,
            Rssi = Rssi,
            IsPaired = IsPaired,
            IsConnected = IsConnected,
            IsConnectedByCurrentApp = IsConnectedByCurrentApp,
            HasConnectableAdvertisement = HasConnectableAdvertisement,
            IsConnectableAdvertisement = IsConnectableAdvertisement,
            IsConnecting = IsConnecting,
            AdvertisementType = AdvertisementType,
            AdvertisementSections = AdvertisementSections
                .Select(section => new BleAdvertisementSection
                {
                    Length = section.Length,
                    AdType = section.AdType,
                    Data = section.Data
                })
                .ToArray()
        };
    }

    public void UpdateFrom(BleDeviceInfo other)
    {
        DeviceId = other.DeviceId;
        Name = other.Name;
        Address = other.Address;
        Rssi = other.Rssi;
        IsPaired = other.IsPaired;
        IsConnected = other.IsConnected;
        IsConnectedByCurrentApp = other.IsConnectedByCurrentApp;
        HasConnectableAdvertisement = other.HasConnectableAdvertisement;
        IsConnectableAdvertisement = other.IsConnectableAdvertisement;
        IsConnecting = other.IsConnecting;
        AdvertisementType = other.AdvertisementType;
        AdvertisementSections = other.AdvertisementSections
            .Select(section => new BleAdvertisementSection
            {
                Length = section.Length,
                AdType = section.AdType,
                Data = section.Data
            })
            .ToArray();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        if (propertyName is nameof(Name) or nameof(Rssi))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SignalText)));
        }

        return true;
    }

    private void OnConnectionOwnershipChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOccupiedByOther)));
    }
}

public sealed class BleDataReceivedEventArgs : EventArgs
{
    public Guid ServiceUuid { get; init; }
    public Guid CharacteristicUuid { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class BleConnectionChangedEventArgs : EventArgs
{
    public string DeviceId { get; init; } = string.Empty;
    public bool IsConnected { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class BleException : Exception
{
    public BleException(string message) : base(message)
    {
    }

    public BleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class BleAccessDeniedException : BleException
{
    public BleAccessDeniedException(string message) : base(message)
    {
    }

    public BleAccessDeniedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

[SupportedOSPlatform("windows10.0.10240.0")]
public sealed class BleManager : IDisposable
{
    private sealed class AdvertisementSnapshot
    {
        public string Address { get; init; } = string.Empty;
        public string LocalName { get; init; } = string.Empty;
        public short Rssi { get; init; }
        public string AdvertisementType { get; init; } = "N/A";
        public bool HasConnectability { get; init; }
        public bool IsConnectable { get; init; }
        public IReadOnlyList<BleAdvertisementSection> Sections { get; init; } =
            Array.Empty<BleAdvertisementSection>();
    }

    private DeviceWatcher? _deviceWatcher;
    private BluetoothLEAdvertisementWatcher? _advertisementWatcher;
    private BluetoothLEDevice? _device;
    private readonly ConcurrentDictionary<string, BleDeviceInfo> _devicesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _deviceIdsByAddress = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, AdvertisementSnapshot> _advertisementsByAddress =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, GattCharacteristic> _subscribedCharacteristics = new();

    private string? _connectedDeviceId;
    private bool _ownsConnection;
    private bool _isReconnecting;
    private bool _disposed;
    private bool _autoReconnect = true;
    private int _reconnectDelayMs = 2000;
    private int _reconnectMaxAttempts = 5;

    public event EventHandler<BleDeviceInfo>? DeviceDiscovered;
    public event EventHandler<BleDeviceInfo>? DeviceUpdated;
    public event EventHandler<BleConnectionChangedEventArgs>? ConnectionChanged;
    public event EventHandler<BleDataReceivedEventArgs>? DataReceived;

    public void StartScan()
    {
        ThrowIfDisposed();
        StopScan();

        _devicesById.Clear();
        _deviceIdsByAddress.Clear();
        _advertisementsByAddress.Clear();

        string[] requestedProperties =
        {
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.Bluetooth.Le.IsConnectable",
            "System.Devices.Aep.SignalStrength"
        };

        string selector =
            $"({BluetoothLEDevice.GetDeviceSelectorFromPairingState(false)}) OR ({BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)})";

        _deviceWatcher = DeviceInformation.CreateWatcher(
            selector,
            requestedProperties,
            DeviceInformationKind.AssociationEndpoint);

        _deviceWatcher.Added += OnDeviceAdded;
        _deviceWatcher.Updated += OnDeviceUpdated;
        _deviceWatcher.Start();

        _advertisementWatcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _advertisementWatcher.Received += OnAdvertisementReceived;
        _advertisementWatcher.Start();
    }

    public void StopScan()
    {
        if (_deviceWatcher != null)
        {
            _deviceWatcher.Added -= OnDeviceAdded;
            _deviceWatcher.Updated -= OnDeviceUpdated;

            if (_deviceWatcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                _deviceWatcher.Stop();
            }

            _deviceWatcher = null;
        }

        if (_advertisementWatcher != null)
        {
            _advertisementWatcher.Received -= OnAdvertisementReceived;

            if (_advertisementWatcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
            {
                _advertisementWatcher.Stop();
            }

            _advertisementWatcher = null;
        }
    }

    public IReadOnlyList<BleDeviceInfo> GetDiscoveredDevices(bool onlyAdvertising = false)
    {
        IEnumerable<BleDeviceInfo> devices = _devicesById.Values;
        if (onlyAdvertising)
        {
            devices = devices.Where(device =>
                device.IsConnected
                || (!string.IsNullOrWhiteSpace(device.Address)
                    && _advertisementsByAddress.ContainsKey(device.Address)));
        }

        return devices.Select(device =>
        {
            device.IsConnectedByCurrentApp = IsConnectionOwnedByCurrentApp(device.DeviceId);
            return device.Clone();
        }).ToList();
    }

    public bool IsConnectionOwnedByCurrentApp(string deviceId)
    {
        return _ownsConnection
            && _device?.ConnectionStatus == BluetoothConnectionStatus.Connected
            && string.Equals(_device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase);
    }

    public BleDeviceInfo? GetCurrentConnectedDevice()
    {
        if (!_ownsConnection || _device?.ConnectionStatus != BluetoothConnectionStatus.Connected)
        {
            return null;
        }

        if (_devicesById.TryGetValue(_device.DeviceId, out BleDeviceInfo? currentDevice))
        {
            currentDevice.IsConnectedByCurrentApp = true;
            return currentDevice.Clone();
        }

        return new BleDeviceInfo
        {
            DeviceId = _device.DeviceId,
            Name = _device.Name ?? string.Empty,
            Address = FormatBluetoothAddress(_device.BluetoothAddress),
            IsConnected = true,
            IsConnectedByCurrentApp = true
        };
    }

    public async Task<bool> PairAsync(string deviceId)
    {
        var deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId)
            ?? throw new BleException($"无法找到设备: {deviceId}");

        if (deviceInfo.Pairing.IsPaired)
        {
            UpdateDevicePairingState(deviceId, true);
            return true;
        }

        if (!deviceInfo.Pairing.CanPair)
        {
            throw new BleException($"设备不支持配对: {deviceInfo.Name} ({deviceId})");
        }

        DevicePairingResult result = await TryPairWithCustomFlowAsync(deviceInfo);
        if (result.Status is DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired)
        {
            UpdateDevicePairingState(deviceId, true);
            return true;
        }

        throw new BleException($"配对失败，状态: {result.Status}");
    }

    public async Task<bool> UnpairAsync(string deviceId)
    {
        var deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId)
            ?? throw new BleException($"无法找到设备: {deviceId}");

        if (!deviceInfo.Pairing.IsPaired)
        {
            UpdateDevicePairingState(deviceId, false);
            return true;
        }

        var result = await deviceInfo.Pairing.UnpairAsync();
        bool ok = result.Status == DeviceUnpairingResultStatus.Unpaired;
        if (ok)
        {
            UpdateDevicePairingState(deviceId, false);
        }

        return ok;
    }

    public async Task ConnectAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_devicesById.TryGetValue(deviceId, out BleDeviceInfo? discoveredDevice)
            && discoveredDevice.IsOccupiedByOther)
        {
            throw new BleException("设备已被其他软件或终端连接，当前不可连接");
        }

        if (_device != null)
        {
            await DisconnectAsync();
        }

        _autoReconnect = true;
        _ownsConnection = false;

        try
        {
            _device = await BluetoothLEDevice.FromIdAsync(deviceId)
                ?? throw new BleException($"无法找到设备: {deviceId}");

            _connectedDeviceId = deviceId;
            _device.ConnectionStatusChanged += OnConnectionStatusChanged;

            await WaitForGattAsync(cancellationToken);
            _ownsConnection = true;
            UpdateDeviceConnectionState(deviceId, true);
        }
        catch
        {
            _ownsConnection = false;
            DisposeDevice();
            _connectedDeviceId = null;
            throw;
        }

        ConnectionChanged?.Invoke(this, new BleConnectionChangedEventArgs
        {
            DeviceId = deviceId,
            IsConnected = true,
            Reason = "Connected"
        });
    }

    public bool IsConnected => _ownsConnection && _device?.ConnectionStatus == BluetoothConnectionStatus.Connected;

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsConnected)
        {
            return;
        }

        var connectedDevice = GetCurrentConnectedDevice();
        if (connectedDevice == null || string.IsNullOrWhiteSpace(connectedDevice.DeviceId))
        {
            throw new BleException("设备未连接");
        }

        await ConnectAsync(connectedDevice.DeviceId, cancellationToken);
    }

    public async Task<IReadOnlyList<BleGattServiceInfo>> GetGattProfileAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);

        var servicesResult = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
        if (servicesResult.Status == GattCommunicationStatus.AccessDenied)
        {
            throw new BleAccessDeniedException("蓝牙访问被拒绝，当前设备可能被其他软件持有");
        }

        if (servicesResult.Status != GattCommunicationStatus.Success)
        {
            throw new BleException($"读取 GATT 服务失败: {servicesResult.Status}");
        }

        var services = new List<BleGattServiceInfo>();
        foreach (GattDeviceService service in servicesResult.Services.OrderBy(item => item.Uuid.ToString()))
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<BleGattCharacteristicInfo> characteristics = Array.Empty<BleGattCharacteristicInfo>();
            var charsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            if (charsResult.Status == GattCommunicationStatus.Success)
            {
                characteristics = charsResult.Characteristics
                    .OrderBy(item => item.Uuid.ToString())
                    .Select(item => new BleGattCharacteristicInfo
                    {
                        Name = LookupCharacteristicName(item.Uuid),
                        ServiceUuid = service.Uuid,
                        Uuid = item.Uuid,
                        Properties = FormatCharacteristicProperties(item.CharacteristicProperties),
                        Handle = item.AttributeHandle,
                        SupportsRead = item.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read),
                        SupportsNotify =
                            item.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                            item.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate),
                        SupportsWrite =
                            item.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
                            item.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)
                    })
                    .ToArray();
            }

            services.Add(new BleGattServiceInfo
            {
                Name = LookupServiceName(service.Uuid),
                Uuid = service.Uuid,
                Characteristics = characteristics
            });
        }

        return services;
    }

    public async Task WriteAsync(Guid serviceUuid, Guid characteristicUuid, byte[] data)
    {
        await EnsureConnectedAsync();

        var characteristic = await GetCharacteristicAsync(serviceUuid, characteristicUuid);

        GattWriteResult result;
        if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))
        {
            result = await characteristic.WriteValueWithResultAsync(data.AsBuffer());
        }
        else if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
        {
            result = await characteristic.WriteValueWithResultAsync(data.AsBuffer(), GattWriteOption.WriteWithoutResponse);
        }
        else
        {
            throw new BleException("该特征不支持写入");
        }

        if (result.Status != GattCommunicationStatus.Success)
        {
            if (result.Status == GattCommunicationStatus.AccessDenied)
            {
                throw new BleAccessDeniedException("蓝牙写入被拒绝，当前设备可能被其他软件持有");
            }

            throw new BleException($"写入失败: {result.Status}");
        }
    }

    public async Task<byte[]> ReadAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        var characteristic = await GetCharacteristicAsync(serviceUuid, characteristicUuid);
        if (!characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
        {
            throw new BleException("该特征不支持读取");
        }

        var result = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
        if (result.Status == GattCommunicationStatus.AccessDenied)
        {
            throw new BleAccessDeniedException("蓝牙读取被拒绝，当前设备可能被其他软件持有");
        }

        if (result.Status != GattCommunicationStatus.Success)
        {
            throw new BleException($"读取失败: {result.Status}");
        }

        return result.Value.ToArray();
    }

    public async Task SubscribeAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        var characteristic = await GetCharacteristicAsync(serviceUuid, characteristicUuid);
        string subscriptionKey = GetSubscriptionKey(serviceUuid, characteristicUuid);

        if (_subscribedCharacteristics.ContainsKey(subscriptionKey))
        {
            return;
        }

        GattClientCharacteristicConfigurationDescriptorValue descriptorValue;
        if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
        {
            descriptorValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
        }
        else if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
        {
            descriptorValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
        }
        else
        {
            throw new BleException("该特征不支持 Notify/Indicate");
        }

        var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(descriptorValue);
        if (status == GattCommunicationStatus.AccessDenied)
        {
            throw new BleAccessDeniedException("蓝牙通知订阅被拒绝，当前设备可能被其他软件持有");
        }

        if (status != GattCommunicationStatus.Success)
        {
            throw new BleException($"订阅失败: {status}");
        }

        characteristic.ValueChanged += OnCharacteristicValueChanged;
        _subscribedCharacteristics[subscriptionKey] = characteristic;
    }

    public async Task UnsubscribeAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        string subscriptionKey = GetSubscriptionKey(serviceUuid, characteristicUuid);
        if (!_subscribedCharacteristics.TryRemove(subscriptionKey, out GattCharacteristic? characteristic))
        {
            return;
        }

        characteristic.ValueChanged -= OnCharacteristicValueChanged;
        await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.None);
    }

    public async Task DisconnectAsync()
    {
        _autoReconnect = false;
        await UnsubscribeAllAsync();
        DisposeDevice();

        if (_connectedDeviceId != null)
        {
            UpdateDeviceConnectionState(_connectedDeviceId, false);
            ConnectionChanged?.Invoke(this, new BleConnectionChangedEventArgs
            {
                DeviceId = _connectedDeviceId,
                IsConnected = false,
                Reason = "ManualDisconnect"
            });
        }
    }

    public void ConfigureReconnect(bool enable, int delayMs = 2000, int maxAttempts = 5)
    {
        _autoReconnect = enable;
        _reconnectDelayMs = delayMs;
        _reconnectMaxAttempts = maxAttempts;
    }

    public async Task ReconnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectedDeviceId))
        {
            throw new BleException("没有记录上次连接的设备 ID");
        }

        await ReconnectLoopAsync(_connectedDeviceId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopScan();
        _autoReconnect = false;
        UnsubscribeAllAsync().GetAwaiter().GetResult();
        DisposeDevice();
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation info)
    {
        BleDeviceInfo device = BuildDeviceInfo(info);
        MergeAdvertisement(device);
        _devicesById[device.DeviceId] = device;

        if (!string.IsNullOrWhiteSpace(device.Address))
        {
            _deviceIdsByAddress[device.Address] = device.DeviceId;
        }

        DeviceDiscovered?.Invoke(this, device.Clone());
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (!_devicesById.TryGetValue(update.Id, out BleDeviceInfo? device))
        {
            return;
        }

        if (update.Properties.TryGetValue("System.Devices.Aep.SignalStrength", out object? rssiValue) && rssiValue != null)
        {
            device.Rssi = Convert.ToInt16(rssiValue);
        }

        if (update.Properties.TryGetValue("System.Devices.Aep.IsConnected", out object? connectedValue) && connectedValue != null)
        {
            device.IsConnected = (bool)connectedValue;
        }

        if (update.Properties.TryGetValue("System.Devices.Aep.Bluetooth.Le.IsConnectable", out object? connectableValue)
            && connectableValue is bool isConnectable)
        {
            device.HasConnectableAdvertisement = true;
            device.IsConnectableAdvertisement = isConnectable;
        }

        device.IsConnectedByCurrentApp = IsConnectionOwnedByCurrentApp(device.DeviceId);

        MergeAdvertisement(device);
        DeviceUpdated?.Invoke(this, device.Clone());
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        string address = FormatBluetoothAddress(args.BluetoothAddress);
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        bool isScanResponse = args.AdvertisementType == BluetoothLEAdvertisementType.ScanResponse;
        _advertisementsByAddress.TryGetValue(address, out AdvertisementSnapshot? previousSnapshot);

        var snapshot = new AdvertisementSnapshot
        {
            Address = address,
            LocalName = args.Advertisement.LocalName ?? string.Empty,
            Rssi = (short)args.RawSignalStrengthInDBm,
            AdvertisementType = isScanResponse && previousSnapshot != null
                ? previousSnapshot.AdvertisementType
                : args.AdvertisementType.ToString(),
            HasConnectability = !isScanResponse || previousSnapshot?.HasConnectability == true,
            IsConnectable = isScanResponse && previousSnapshot != null
                ? previousSnapshot.IsConnectable
                : IsConnectableAdvertisementType(args.AdvertisementType),
            Sections = BuildAdvertisementSections(args.Advertisement)
        };

        _advertisementsByAddress[address] = snapshot;

        if (_deviceIdsByAddress.TryGetValue(address, out string? deviceId)
            && _devicesById.TryGetValue(deviceId, out BleDeviceInfo? device))
        {
            ApplyAdvertisement(device, snapshot);
            DeviceUpdated?.Invoke(this, device.Clone());
        }
    }

    private async Task WaitForGattAsync(CancellationToken cancellationToken, int retries = 5)
    {
        if (_device == null)
        {
            throw new BleException("设备未连接");
        }

        for (int attempt = 0; attempt < retries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.AccessDenied)
            {
                throw new BleAccessDeniedException("蓝牙访问被拒绝，当前设备可能被其他软件持有");
            }

            if (result.Status == GattCommunicationStatus.Success)
            {
                return;
            }

            await Task.Delay(300, cancellationToken);
        }

        throw new BleException("连接失败，无法获取 GATT 服务");
    }

    private async Task UnsubscribeAllAsync()
    {
        foreach ((_, GattCharacteristic characteristic) in _subscribedCharacteristics)
        {
            try
            {
                characteristic.ValueChanged -= OnCharacteristicValueChanged;
                await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch
            {
                // Ignore cleanup failures during disconnect.
            }
        }

        _subscribedCharacteristics.Clear();
    }

    private void DisposeDevice()
    {
        _ownsConnection = false;
        if (_device == null)
        {
            return;
        }

        _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _device.Dispose();
        _device = null;
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        bool isConnected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
        if (!isConnected)
        {
            _ownsConnection = false;
        }
        UpdateDeviceConnectionState(sender.DeviceId, isConnected);

        ConnectionChanged?.Invoke(this, new BleConnectionChangedEventArgs
        {
            DeviceId = sender.DeviceId,
            IsConnected = isConnected,
            Reason = isConnected ? "Connected" : "Disconnected"
        });

        if (!isConnected && _autoReconnect && !_isReconnecting && !string.IsNullOrWhiteSpace(_connectedDeviceId))
        {
            _ = ReconnectLoopAsync(_connectedDeviceId);
        }
    }

    private async Task ReconnectLoopAsync(string deviceId)
    {
        if (_isReconnecting)
        {
            return;
        }

        _isReconnecting = true;
        try
        {
            int attempt = 0;
            while (_autoReconnect && (_reconnectMaxAttempts < 0 || attempt < _reconnectMaxAttempts))
            {
                attempt++;
                try
                {
                    await Task.Delay(_reconnectDelayMs);
                    DisposeDevice();

                    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await ConnectAsync(deviceId, cancellation.Token);
                    return;
                }
                catch (Exception ex)
                {
                    ConnectionChanged?.Invoke(this, new BleConnectionChangedEventArgs
                    {
                        DeviceId = deviceId,
                        IsConnected = false,
                        Reason = $"Reconnect #{attempt} failed: {ex.Message}"
                    });
                }
            }

            ConnectionChanged?.Invoke(this, new BleConnectionChangedEventArgs
            {
                DeviceId = deviceId,
                IsConnected = false,
                Reason = "自动重连已停止"
            });
        }
        finally
        {
            _isReconnecting = false;
        }
    }

    private async Task<GattCharacteristic> GetCharacteristicAsync(Guid serviceUuid, Guid characteristicUuid)
    {
        if (_device == null)
        {
            throw new BleException("设备未连接");
        }

        var servicesResult = await _device.GetGattServicesForUuidAsync(serviceUuid, BluetoothCacheMode.Cached);
        if (servicesResult.Status == GattCommunicationStatus.AccessDenied)
        {
            throw new BleAccessDeniedException("蓝牙访问服务被拒绝，当前设备可能被其他软件持有");
        }

        if (servicesResult.Status != GattCommunicationStatus.Success || !servicesResult.Services.Any())
        {
            throw new BleException($"未找到服务: {serviceUuid}");
        }

        GattDeviceService service = servicesResult.Services.First();
        var charsResult = await service.GetCharacteristicsForUuidAsync(characteristicUuid, BluetoothCacheMode.Cached);
        if (charsResult.Status == GattCommunicationStatus.AccessDenied)
        {
            throw new BleAccessDeniedException("蓝牙访问特征被拒绝，当前设备可能被其他软件持有");
        }

        if (charsResult.Status != GattCommunicationStatus.Success || !charsResult.Characteristics.Any())
        {
            throw new BleException($"未找到特征: {characteristicUuid}");
        }

        return charsResult.Characteristics.First();
    }

    private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        DataReceived?.Invoke(this, new BleDataReceivedEventArgs
        {
            ServiceUuid = sender.Service.Uuid,
            CharacteristicUuid = sender.Uuid,
            Data = args.CharacteristicValue.ToArray(),
            Timestamp = args.Timestamp
        });
    }

    private static async Task<DevicePairingResult> TryPairWithCustomFlowAsync(DeviceInformation deviceInfo)
    {
        DeviceInformationCustomPairing customPairing = deviceInfo.Pairing.Custom;
        TypedEventHandler<DeviceInformationCustomPairing, DevicePairingRequestedEventArgs> handler = OnPairingRequested;

        customPairing.PairingRequested += handler;
        try
        {
            DevicePairingResult result = await customPairing.PairAsync(
                DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch,
                DevicePairingProtectionLevel.None);

            if (result.Status == DevicePairingResultStatus.Failed)
            {
                result = await deviceInfo.Pairing.PairAsync(DevicePairingProtectionLevel.None);
            }

            return result;
        }
        finally
        {
            customPairing.PairingRequested -= handler;
        }
    }

    private static void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
    {
        if (args.PairingKind is DevicePairingKinds.ConfirmOnly or DevicePairingKinds.ConfirmPinMatch)
        {
            args.Accept();
        }
    }

    private BleDeviceInfo BuildDeviceInfo(DeviceInformation info)
    {
        short rssi = 0;
        if (info.Properties.TryGetValue("System.Devices.Aep.SignalStrength", out object? rssiValue) && rssiValue != null)
        {
            rssi = Convert.ToInt16(rssiValue);
        }

        bool isConnected = false;
        if (info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out object? connectedValue) && connectedValue != null)
        {
            isConnected = (bool)connectedValue;
        }

        bool hasConnectableState = false;
        bool isConnectable = false;
        if (info.Properties.TryGetValue("System.Devices.Aep.Bluetooth.Le.IsConnectable", out object? connectableValue)
            && connectableValue is bool connectable)
        {
            hasConnectableState = true;
            isConnectable = connectable;
        }

        string address = string.Empty;
        if (info.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out object? addressValue) && addressValue != null)
        {
            address = NormalizeAddress(addressValue.ToString());
        }

        return new BleDeviceInfo
        {
            DeviceId = info.Id,
            Name = info.Name ?? string.Empty,
            Address = address,
            Rssi = rssi,
            IsPaired = info.Pairing.IsPaired,
            IsConnected = isConnected,
            IsConnectedByCurrentApp = IsConnectionOwnedByCurrentApp(info.Id),
            HasConnectableAdvertisement = hasConnectableState,
            IsConnectableAdvertisement = isConnectable,
            AdvertisementType = "N/A",
            AdvertisementSections = Array.Empty<BleAdvertisementSection>()
        };
    }

    private void MergeAdvertisement(BleDeviceInfo device)
    {
        if (string.IsNullOrWhiteSpace(device.Address))
        {
            return;
        }

        if (_advertisementsByAddress.TryGetValue(device.Address, out AdvertisementSnapshot? snapshot))
        {
            ApplyAdvertisement(device, snapshot);
        }
    }

    private static void ApplyAdvertisement(BleDeviceInfo device, AdvertisementSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.LocalName))
        {
            device.Name = snapshot.LocalName;
        }

        device.Rssi = snapshot.Rssi;
        device.AdvertisementType = snapshot.AdvertisementType;
        if (snapshot.HasConnectability)
        {
            device.HasConnectableAdvertisement = true;
            device.IsConnectableAdvertisement = snapshot.IsConnectable;
        }
        device.AdvertisementSections = snapshot.Sections
            .Select(section => new BleAdvertisementSection
            {
                Length = section.Length,
                AdType = section.AdType,
                Data = section.Data
            })
            .ToArray();
    }

    private void UpdateDevicePairingState(string deviceId, bool isPaired)
    {
        if (_devicesById.TryGetValue(deviceId, out BleDeviceInfo? device))
        {
            device.IsPaired = isPaired;
            DeviceUpdated?.Invoke(this, device.Clone());
        }
    }

    private void UpdateDeviceConnectionState(string deviceId, bool isConnected)
    {
        if (_devicesById.TryGetValue(deviceId, out BleDeviceInfo? device))
        {
            device.IsConnected = isConnected;
            device.IsConnectedByCurrentApp = isConnected && IsConnectionOwnedByCurrentApp(deviceId);
            DeviceUpdated?.Invoke(this, device.Clone());
        }
    }

    private static IReadOnlyList<BleAdvertisementSection> BuildAdvertisementSections(BluetoothLEAdvertisement advertisement)
    {
        return advertisement.DataSections.Select(section =>
        {
            byte[] bytes = section.Data.ToArray();
            return new BleAdvertisementSection
            {
                Length = bytes.Length + 1,
                AdType = FormatAdType(section.DataType),
                Data = bytes.Length == 0 ? "(empty)" : Convert.ToHexString(bytes)
            };
        }).ToArray();
    }

    private static bool IsConnectableAdvertisementType(BluetoothLEAdvertisementType advertisementType)
    {
        return advertisementType is BluetoothLEAdvertisementType.ConnectableUndirected
            or BluetoothLEAdvertisementType.ConnectableDirected;
    }

    private static string FormatBluetoothAddress(ulong address)
    {
        return string.Join(
            ":",
            Enumerable.Range(0, 6)
                .Select(index => ((address >> ((5 - index) * 8)) & 0xFF).ToString("X2")));
    }

    private static string NormalizeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string hex = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (hex.Length < 12)
        {
            return value.ToUpperInvariant();
        }

        hex = hex[^12..];
        return string.Join(":", Enumerable.Range(0, 6).Select(index => hex.Substring(index * 2, 2)));
    }

    private static string FormatAdType(byte dataType)
    {
        return dataType switch
        {
            0x01 => "Flags (0x01)",
            0x02 => "Incomplete 16-bit UUIDs (0x02)",
            0x03 => "Complete 16-bit UUIDs (0x03)",
            0x08 => "Shortened Local Name (0x08)",
            0x09 => "Complete Local Name (0x09)",
            0x0A => "Tx Power (0x0A)",
            0x16 => "Service Data (0x16)",
            0x19 => "Appearance (0x19)",
            0xFF => "Manufacturer Data (0xFF)",
            _ => $"Type 0x{dataType:X2}"
        };
    }

    private static string LookupServiceName(Guid uuid)
    {
        return NormalizeUuid(uuid) switch
        {
            "00001800-0000-1000-8000-00805F9B34FB" => "GenericAccess",
            "00001801-0000-1000-8000-00805F9B34FB" => "GenericAttribute",
            "0000180A-0000-1000-8000-00805F9B34FB" => "DeviceInformation",
            "0000180F-0000-1000-8000-00805F9B34FB" => "BatteryService",
            _ => "Unknown Service"
        };
    }

    private static string LookupCharacteristicName(Guid uuid)
    {
        return NormalizeUuid(uuid) switch
        {
            "00002A00-0000-1000-8000-00805F9B34FB" => "DeviceName",
            "00002A01-0000-1000-8000-00805F9B34FB" => "Appearance",
            "00002A04-0000-1000-8000-00805F9B34FB" => "PeripheralPreferredConnectionParameters",
            "00002A05-0000-1000-8000-00805F9B34FB" => "ServiceChanged",
            "00002A19-0000-1000-8000-00805F9B34FB" => "BatteryLevel",
            _ => "Characteristic"
        };
    }

    private static string NormalizeUuid(Guid uuid) => uuid.ToString().ToUpperInvariant();

    private static string FormatCharacteristicProperties(GattCharacteristicProperties properties)
    {
        var flags = new List<string>();

        if (properties.HasFlag(GattCharacteristicProperties.Read))
        {
            flags.Add("Read");
        }

        if (properties.HasFlag(GattCharacteristicProperties.Write))
        {
            flags.Add("Write");
        }

        if (properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
        {
            flags.Add("WriteWithoutResponse");
        }

        if (properties.HasFlag(GattCharacteristicProperties.Notify))
        {
            flags.Add("Notify");
        }

        if (properties.HasFlag(GattCharacteristicProperties.Indicate))
        {
            flags.Add("Indicate");
        }

        if (flags.Count == 0)
        {
            flags.Add("None");
        }

        return string.Join(", ", flags);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BleManager));
        }
    }

    private static string GetSubscriptionKey(Guid serviceUuid, Guid characteristicUuid)
    {
        return $"{serviceUuid:B}|{characteristicUuid:B}";
    }
}

[SupportedOSPlatform("windows10.0.10240.0")]
public static class BleToolKit
{
    private static readonly Lazy<BleManager> _shared = new(() => new BleManager());

    public static BleManager Shared => _shared.Value;

    public static BleManager Create() => new();
}
