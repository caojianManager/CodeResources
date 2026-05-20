using System.Windows.Media;

namespace FrameWork.Helper
{
    public static class SolidColorBrushHelper
    {
        public static SolidColorBrush CreateBrushFromHex(string hexColor)
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);  // 转换 Hex 为 Color
            return new SolidColorBrush(color);  // 返回 SolidColorBrush
        }
    }
}
