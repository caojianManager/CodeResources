using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FrameWork.Themes.ComponentResourceKeys
{
    public static class Colors
    {
        public static ComponentResourceKey RedColor => new ComponentResourceKey(typeof(Colors), "RedColor");
        public static ComponentResourceKey YellowColor => new ComponentResourceKey(typeof(Colors), "YellowColor");
        public static ComponentResourceKey GreenColor => new ComponentResourceKey(typeof(Colors), "GreenColor");
        public static ComponentResourceKey BrownColor => new ComponentResourceKey(typeof(Colors), "BrownColor");
        public static ComponentResourceKey BlueColor => new ComponentResourceKey(typeof(Colors), "BlueColor");
        public static ComponentResourceKey DarkBlueColor => new ComponentResourceKey(typeof(Colors), "DarkBlueColor");
        public static ComponentResourceKey BlueGrayColor => new ComponentResourceKey(typeof(Colors), "BlueGrayColor");
        public static ComponentResourceKey OrangeColor => new ComponentResourceKey(typeof(Colors), "OrangeColor");
    }
}
