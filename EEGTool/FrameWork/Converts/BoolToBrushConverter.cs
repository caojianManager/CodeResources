using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = Brushes.DodgerBlue; 
        public Brush FalseBrush { get; set; } = Brushes.Black; 

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
                return flag ? TrueBrush : FalseBrush;

            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return default;
        }
    }
}
