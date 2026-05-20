using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FrameWork.Themes.ComponentResourceKeys
{
    public static class Brushes
    {
        public static ComponentResourceKey WindowBackgroundImage => new ComponentResourceKey(typeof(Brushes), "WindowBackgroundImage");
        public static ComponentResourceKey BrushesBlue => new ComponentResourceKey(typeof(Brushes), "BrushesBlue");
    }
}
