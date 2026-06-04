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


        public double Impedance_BandPassLow = 21.0;
        public double Impedance_BandPassHeigh = 41.0;
        public double AA = 10e-9;
        public int series_resistor_kohm = 10;


        public bool IsUseProxy = false;
        public string ProxyAddress = "http://192.168.100.33:9090";
        public string HTTP_IP = "192.168.1.104";
        public int HTTP_PORT = 49090;
        public double ReferenceVoltage = 4.5;

        public string InitSampleRate = "250Hz";            //采样率
        public int SampleRate = 500;            //采样率
        public string CollectionSelectedSampleRate = "250Hz";
        public string CollectionSelectedTemplate = string.Empty;
        public bool CollectionIsVideoRecordYes = false;
        public int DefaultTimeWindowSec = 20;   //默认时间窗口大小
        public int ChannelCount = 16;           //通道数量
        public int GainNum = 6;                 //放大倍数
        public bool IsEegAuto = true;          //Eeg时序图是否开启自动纵轴

        //滤波
        public bool IsOpenBandPass = false;     //带通开关
        public bool IsOpenNotchFilter = false;  //陷波开关
        public double BandPassLow = 0.5;
        public double BandPassHeigh = 50;
        public int NotchFilterHz = 0;           // 0-None,1-50Hz 2-60Hz



        //设置
        public int ImpedanceMaxValue = 0;
        public int ImpedanceMinValue = 0;
        public bool IsCanImpeDance = false;
        //医院
        public string HospitalName = string.Empty;
        public string HospitalAddress = string.Empty;
        public string HospitalPhone = string.Empty;
        public string HospitalIntroduce = string.Empty;




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
