using BLETool;
using FrameWork.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EEGTool.Models.BLE
{
    public sealed class BleDataChannelInfo
    {
        public BleDataChannelInfo(
            BleGattCharacteristicInfo writeCharacteristic,
            BleGattCharacteristicInfo notifyCharacteristic,
            bool isNotifySubscribed)
        {
            WriteCharacteristic = writeCharacteristic;
            NotifyCharacteristic = notifyCharacteristic;
            IsNotifySubscribed = isNotifySubscribed;
        }

        public BleGattCharacteristicInfo WriteCharacteristic { get; }
        public BleGattCharacteristicInfo NotifyCharacteristic { get; }
        public bool IsNotifySubscribed { get; }
    }

    public static class BleGattProfileHelper
    {
        public static readonly Guid TargetServiceUuid =
            Guid.Parse("0003cdd0-0000-1000-8000-00805f9b0131");

        public static async Task<BleDataChannelInfo?> GetDataChannelAsync(
            BleManager ble,
            bool continueWhenNotifyAccessDenied = false)
        {
            IReadOnlyList<BleGattServiceInfo> gatt = await ble.GetGattProfileAsync();
            LogGattProfile(gatt);

            var targetService = gatt.FirstOrDefault(service => service.Uuid == TargetServiceUuid);
            if (targetService == null)
            {
                Logger.Debug($"[BleGattProfileHelper][GetDataChannelAsync]:没有找到目标服务 {TargetServiceUuid}");
                return null;
            }

            var allCharacteristics = gatt.SelectMany(service => service.Characteristics).ToList();
            var writeCharacteristic = targetService.Characteristics.FirstOrDefault(characteristic => characteristic.SupportsWrite)
                ?? allCharacteristics.FirstOrDefault(characteristic => characteristic.SupportsWrite);
            var notifyCharacteristic = targetService.Characteristics.FirstOrDefault(characteristic => characteristic.SupportsNotify)
                ?? allCharacteristics.FirstOrDefault(characteristic => characteristic.SupportsNotify);

            if (writeCharacteristic == null || notifyCharacteristic == null)
            {
                Logger.Debug("[BleGattProfileHelper][GetDataChannelAsync]:没有找到写入或通知特征");
                return null;
            }

            bool isNotifySubscribed = false;
            try
            {
                await ble.SubscribeAsync(notifyCharacteristic.ServiceUuid, notifyCharacteristic.Uuid);
                isNotifySubscribed = true;
                Logger.Info($"[BleGattProfileHelper][GetDataChannelAsync]:Notify订阅成功 Service={notifyCharacteristic.ServiceUuid}, Characteristic={notifyCharacteristic.Uuid}");
            }
            catch (BleAccessDeniedException ex) when (continueWhenNotifyAccessDenied)
            {
                Logger.Debug($"[BleGattProfileHelper][GetDataChannelAsync]:Notify订阅访问被拒绝，继续使用写入特征。Service={notifyCharacteristic.ServiceUuid}, Characteristic={notifyCharacteristic.Uuid}, Error={ex}");
            }
            catch (UnauthorizedAccessException ex) when (continueWhenNotifyAccessDenied)
            {
                Logger.Debug($"[BleGattProfileHelper][GetDataChannelAsync]:Notify订阅无权限，继续使用写入特征。Service={notifyCharacteristic.ServiceUuid}, Characteristic={notifyCharacteristic.Uuid}, Error={ex}");
            }

            return new BleDataChannelInfo(
                writeCharacteristic,
                notifyCharacteristic,
                isNotifySubscribed);
        }

        public static BleGattCharacteristicInfo? FindWriteCharacteristic(
            IReadOnlyList<BleGattServiceInfo> gatt)
        {
            var targetService = gatt.FirstOrDefault(service => service.Uuid == TargetServiceUuid);
            return targetService?.Characteristics.FirstOrDefault(characteristic => characteristic.SupportsWrite)
                ?? gatt.SelectMany(service => service.Characteristics)
                    .FirstOrDefault(characteristic => characteristic.SupportsWrite);
        }

        private static void LogGattProfile(IReadOnlyList<BleGattServiceInfo> gatt)
        {
            Logger.Info($"[BleGattProfileHelper]:GATT服务数量 {gatt.Count}");
            foreach (var service in gatt)
            {
                Logger.Info($"[BleGattProfileHelper]:Service={service.Uuid}, Characteristics={service.Characteristics.Count}");
                foreach (var characteristic in service.Characteristics)
                {
                    Logger.Info($"[BleGattProfileHelper]:  Characteristic={characteristic.Uuid}, Properties={characteristic.Properties}, SupportsWrite={characteristic.SupportsWrite}, SupportsNotify={characteristic.SupportsNotify}");
                }
            }
        }
    }
}
