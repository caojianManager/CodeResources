using System;

namespace EEGTool.CommunicationStrategy
{
    public static class CommunicationStrategyFactory
    {
        public static ICommunicationStrategy Create(CommunicationType type)
        {
            return type switch
            {
                CommunicationType.Ble => new BleCommunicationStrategy(),
                CommunicationType.Cdc => new CdcCommunicationStrategy(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "不支持的通信类型")
            };
        }
    }
}
