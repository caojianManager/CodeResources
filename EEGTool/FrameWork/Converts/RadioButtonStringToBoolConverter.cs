using System.Globalization;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class RadioButtonStringToBoolConverter : IValueConverter
    {
        // value 是 SelectedFrequency， parameter 是 RadioButton 的标识（如 "50Hz"）
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value?.ToString() == parameter?.ToString());
        }

        // 当RadioButton选中时，把 parameter 的值传给 SelectedFrequency
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return parameter.ToString();
            return Binding.DoNothing;
        }
    }
}
