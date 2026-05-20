using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FrameWork.Converts
{
    public class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // 验证源值（实际宽度）和参数（比例）是否有效
            if (value is double actualWidth && double.TryParse(parameter.ToString(), out double ratio))
            {
                // 微调：减去20抵消上级Grid的Margin（38,42），避免列宽超出容器出现横向滚动
                return Math.Max(0, actualWidth * ratio - 20);
            }
            return 0;
        }

        // 反向转换（无需实现，GridViewColumn宽度无需反向绑定）
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
