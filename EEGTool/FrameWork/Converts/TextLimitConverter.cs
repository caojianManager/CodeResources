using System.Globalization;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class TextLimitConverter : IValueConverter
    {
        public int MaxLength { get; set; } = 50;
        public int MaxValue { get; set; } = 999;
        public int MinValue { get; set; } = 1;


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is int number)
            {
                number = Math.Max(MinValue, Math.Min(MaxValue, number));
                return number.ToString();
            }
            else if(value is string text)
            {
                return text.Length > MaxLength ? text.Substring(0, MaxLength) + "..." : text;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
