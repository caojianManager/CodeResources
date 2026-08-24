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

namespace EEGTool.Views.YelloSpot
{
    /// <summary>
    /// YelloSpotCaptureView.xaml 的交互逻辑
    /// </summary>
    public partial class YelloSpotCaptureView : UserControl
    {
        public YelloSpotCaptureView()
        {
            InitializeComponent();
        }

        //Step 2: 初始化绘制所需要的资源
        private void OpenTkControl_Init()
        {
        }

        //Step 3:渲染帧
        private void OpenTkControl_OnRender(TimeSpan obj)
        {
        }
    }
}
