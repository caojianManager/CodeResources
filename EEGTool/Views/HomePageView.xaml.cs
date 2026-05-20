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
using System.Windows.Shapes;
using FrameWork.View;

namespace EEGTool.Views
{
    /// <summary>
    /// HomePageView.xaml 的交互逻辑
    /// </summary>
    public partial class HomePageView : BaseWindow
    {
        public HomePageView()
        {
            InitializeComponent();
        }

        private void OuterBorder_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            OuterBorderClip.Rect = new Rect(0, 0, OuterBorder.ActualWidth, OuterBorder.ActualHeight);
        }
    }
}
