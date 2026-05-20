using System.Globalization;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class IntToInverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;  // 返回布尔值
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;  // 无需实现反向转换
        }
    }
}
