using System.IO;
using ScColor = ScottPlot.Color;

namespace FrameWork.Common
{
    public static class Constants
    {
        public static readonly string ShaderResoucesPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "shaders"));

        public static int WindowMaxSeconds = 10;                     //最长数据长度
        public static int WindowSeconds = 2;                        //时间窗口
        public static int VertScale = 10;                           //默认EEG图表每个通道的高度阈值
        public static int SampleIntervalMs = 60;                    //采样间隔时间
        public static int WindowSecondsMax = 15;
        public const int BufferWindowSec = 30 ;
        public const int SERIES_RESISTOR_KOHM = 10;                 //P端通道上串联电阻大小
        public static int CurrentElectricQuantity = 0;              //当前电池电量
        public static string RootRecordFolder = "Records";          //相对路径文件夹

        public static readonly List<string> ChannelList = new List<string>()
        {
            "Ch1","Ch2","Ch3","Ch4",
            "Ch5","Ch6","Ch7","Ch8",
            "Ch9","Ch10","Ch11","Ch12",
            "Ch13","Ch14","Ch15","Ch16",
        };

        public static readonly ScColor[] ChannelColors = new ScColor[]
        {
            ScColor.FromHtml("#FF4500"), ScColor.FromHtml("#32CD32"), ScColor.FromHtml("#DAA520"), ScColor.FromHtml("#C71585"),
            ScColor.FromHtml("#008080"), ScColor.FromHtml("#FF7F50"), ScColor.FromHtml("#7B68EE"), ScColor.FromHtml("#1E90FF"),
            ScColor.FromHtml("#FFD700"), ScColor.FromHtml("#8A2BE2"), ScColor.FromHtml("#A52A2A"), ScColor.FromHtml("#F08080"),
            ScColor.FromHtml("#20B2AA"), ScColor.FromHtml("#C0C0C0"), ScColor.FromHtml("#FF1493"), ScColor.FromHtml("#00BFFF")
        };
    }
}
