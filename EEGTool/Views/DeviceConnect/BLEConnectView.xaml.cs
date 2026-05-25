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
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using EEGTool.ViewModels.DeviceConnect;

namespace EEGTool.Views.DeviceConnect
{
    /// <summary>
    /// BLEConnectView.xaml 的交互逻辑
    /// </summary>
    public partial class BLEConnectView : UserControl
    {
        public BLEConnectView()
        {
            InitializeComponent();
            Unloaded += BLEConnectView_Unloaded;
        }

        private async void BLEConnectView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DeviceConnectViewModel vm)
            {
                await vm.OnViewUnloadedAsync();
            }
        }

        private void ScanButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            AnimateScanButtonScale(0.96);
        }

        private void ScanButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            AnimateScanButtonScale(1.0);
        }

        private void ScanButton_MouseLeave(object sender, MouseEventArgs e)
        {
            AnimateScanButtonScale(1.0);
        }

        private void AnimateScanButtonScale(double targetScale)
        {
            if (ScanButtonBorder.RenderTransform is not ScaleTransform scale)
            {
                return;
            }

            var duration = TimeSpan.FromMilliseconds(90);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = easing });
        }
    }
}
