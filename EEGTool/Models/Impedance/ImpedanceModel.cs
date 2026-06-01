using FrameWork.Common;
using System.Windows.Media;

namespace EEGTool.Model.Impedance
{
    public class ImpedanceModel
    {
        public string name { get; set; } = string.Empty;
        public string chName { get; set; } = string.Empty;
        public double ImpedanceValue { get; set; } = 0;

        public Brush Color
        {
            get 
            {
                if (0 <= ImpedanceValue && ImpedanceValue <= Config.Instance.ImpedanceMinValue)
                {
                    // 自定义绿色 #00BD48
                    return new SolidColorBrush(ColorConverter.ConvertFromString("#00BD48") as Color? ?? Colors.Green);
                }
                else if (Config.Instance.ImpedanceMinValue < ImpedanceValue && ImpedanceValue <= Config.Instance.ImpedanceMaxValue)
                {
                    // 自定义黄色 #F08B11
                    return new SolidColorBrush(ColorConverter.ConvertFromString("#F08B11") as Color? ?? Colors.Yellow);
                }
                else 
                {
                    // 自定义红色 #EB1515
                    return new SolidColorBrush(ColorConverter.ConvertFromString("#EB1515") as Color? ?? Colors.Red);
                }

            }
        }
    }
}
