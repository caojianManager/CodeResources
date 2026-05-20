using System.Globalization;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class RatioWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter != null && double.TryParse(parameter.ToString(), out double ratio))
            {
                return width / ratio;
            }

            return value; // fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // 一般不需要反向转换
        }
    }

}
