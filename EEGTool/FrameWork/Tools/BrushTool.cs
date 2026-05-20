using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EegAcquisitionSystem.FrameWork.Tools
{
    public class BrushTool
    {

        public static string BrushToHex(Brush brush)
        {
            if (brush is SolidColorBrush solid)
            {
                Color c = solid.Color;
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            }

            return "#00000000";
        }

        public static Brush HexToBrush(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Brushes.Transparent;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }
}
