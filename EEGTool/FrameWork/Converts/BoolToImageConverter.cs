using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FrameWork.Converts
{
    public class BoolToImageConverter : IValueConverter
    {
        public ImageSource TrueImageSource { get; set; }
        public ImageSource FalseImageSource { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
                return flag ? TrueImageSource : FalseImageSource;

            return FalseImageSource;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return default;
        }
    }
}
