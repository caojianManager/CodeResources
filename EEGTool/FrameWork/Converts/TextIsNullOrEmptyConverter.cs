using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class TextIsNullOrEmptyConverter : IValueConverter
    {
        public bool Inverse { get; set; } // 可选：是否反转逻辑

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            bool isNullOrEmpty = false;

            if (value == null)
            {
                isNullOrEmpty = true;
            }
            else if (value is string s)
            {
                isNullOrEmpty = string.IsNullOrWhiteSpace(s);
            }

            if (Inverse)
                isNullOrEmpty = !isNullOrEmpty;

            return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }


}
