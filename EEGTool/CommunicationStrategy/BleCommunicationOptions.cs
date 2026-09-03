using System;

namespace EEGTool.CommunicationStrategy
{
    public sealed class BleCommunicationOptions : CommunicationOptions
    {
        public override CommunicationType Type => CommunicationType.Ble;

        public string DeviceId { get; set; } = string.Empty;

        public bool ContinueWhenNotifyAccessDenied { get; set; } = true;

        public bool EnableReconnect { get; set; } = true;

        public int ReconnectDelayMs { get; set; } = 2000;

        public int ReconnectMaxAttempts { get; set; } = 5;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DeviceId))
            {
                throw new ArgumentException("蓝牙设备 ID 不能为空", nameof(DeviceId));
            }
        }
    }
}
