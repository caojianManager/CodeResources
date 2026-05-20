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
using WpfAnimatedGif;

namespace EEGTool.Views
{
    /// <summary>
    /// StartupView.xaml 的交互逻辑
    /// </summary>
    public partial class StartupView : Window
    {
        public StartupView()
        {
            InitializeComponent();

            //初始化窗口配置
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            this.Loaded += StartupView_Loaded;
        }

        private void StartupView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadGifStartupAni();
        }

        private void LoadGifStartupAni()
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/EEGTool;component/Resources/images/obci_cog_anim-darkblue.gif");
            image.EndInit();
            ImageBehavior.SetAnimatedSource(StartupAniImage, image);
        }
    }
}
