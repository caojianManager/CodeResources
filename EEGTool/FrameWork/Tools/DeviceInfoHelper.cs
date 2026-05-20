using System.Management;
using System.Security.Cryptography;
using System.Text;


namespace FrameWork.Tools
{
    public static class DeviceInfoHelper
    {
        public static string GetDeviceUniqueId()
        {
            string bios = GetWmiProperty("Win32_BIOS", "SerialNumber");
            string board = GetWmiProperty("Win32_BaseBoard", "SerialNumber");
            string cpu = GetWmiProperty("Win32_Processor", "ProcessorId");
            string disk = GetWmiProperty("Win32_PhysicalMedia", "SerialNumber");

            string raw = $"{bios}|{board}|{cpu}|{disk}";
            return ComputeSha256Hash(raw);
        }

        private static string GetWmiProperty(string wmiClass, string wmiProperty)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {wmiProperty} FROM {wmiClass}");
                var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                return obj?[wmiProperty]?.ToString()?.Trim() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string ComputeSha256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
