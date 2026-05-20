using System.IO;
using System.Text.Json;

namespace FrameWork.Common
{

    public class SystemInfo
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsRemember { get; set; } = false;
    }

    public class SystemConfig : Singleton<SystemConfig>
    {
        private static string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DrugControlApp");

        private readonly string ConfigFile = Path.Combine(ConfigDir, "system_config.json");

        private SystemInfo _systemInfoObj = new SystemInfo();
        public SystemInfo SystemInfoObj
        {
            get => _systemInfoObj;
        }

        public void LoadConfig()
        {
            if (!File.Exists(ConfigFile)) 
                return;
            var json = File.ReadAllText(ConfigFile);
            _systemInfoObj = JsonSerializer.Deserialize<SystemInfo>(json);
        }

        public bool SaveConfig()
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            var json = JsonSerializer.Serialize(_systemInfoObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
            return true;
        }

        public bool IsExists(){

            return File.Exists(ConfigFile);
        }
    }

}
