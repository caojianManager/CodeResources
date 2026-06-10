using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EEGTool.ViewModels.Collection;

namespace EEGTool.Views.Collection
{
    /// <summary>
    /// EEGMonitorView.xaml 的交互逻辑
    /// </summary>
    public partial class EEGMonitorView : UserControl
    {
        public EEGMonitorView()
        {
            InitializeComponent();
        }

        private void EegPlot_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is EEGMonitorViewModel viewModel)
            {
                viewModel.ZoomYAxisByWheel(e.Delta);
                e.Handled = true;
            }
        }
    }
}
