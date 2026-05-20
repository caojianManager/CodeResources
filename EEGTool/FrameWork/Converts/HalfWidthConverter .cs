using System.Globalization;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class HalfWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double fullWidth)
                return fullWidth / 2;
            return 100;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

}