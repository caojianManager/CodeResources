using System.IO;
using Framework.Event;
using FrameWork.Event;
using Newtonsoft.Json;


namespace FrameWork.Common
{
    public class Config
    {
        private static string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private static Config _instance;
        public static Config Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }


        /*持久化信息存储*/

        /*---------采集配置相关的属性-------------*/
        public string CollectionSelectedSampleRate = "250Hz";
        public string CollectionSelectedTemplate = string.Empty;
        public bool CollectionIsVideoRecordYes = false;
        public double ReferenceVoltage = 4.5;
        public int GainNum = 24;
        public int Impedance_SampleRate = 250;
        public double Impedance_TargetFreq = 31.25;
        public double Lead_Of = 1.0e-8;
        public double series_resistor_kohm = 10;

        public void Init()
        {
            if (!File.Exists(_configFilePath))
            {
                Save();
            }
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(_configFilePath, json);
        
        }

        private static Config Load()
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            return new Config();
        }

    }
    
}
