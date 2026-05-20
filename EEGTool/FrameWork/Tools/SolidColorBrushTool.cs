using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EegAcquisitionSystem.FrameWork.Tools
{
    public class SolidColorBrushTool
    {
        public static string BrushToHex(Brush brush)
        {
            if (brush is SolidColorBrush solid)
            {
                return solid.Color.ToString(); // #AARRGGBB
            }
            return string.Empty;
        }

    }
}
