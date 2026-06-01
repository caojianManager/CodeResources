using EegAcquisitionSystem;
using FrameWork.View;
using MahApps.Metro.IconPacks;
using OpenTK.Windowing.Common;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WindowState = System.Windows.WindowState;

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
            StateChanged += (_, _) => UpdateWindowChromeState();
            Loaded += HomePageView_Loaded;
            Closed += HomePageView_Closed;
            UpdateWindowChromeState();
        }

        private void HomePageView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadGifStartupAni();
        }

        private void LoadGifStartupAni()
        {
            const double animationSpeedRatio = 1;

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/EEGTool;component/Resources/images/OpenBCI-LoadingGIF-blue-256.gif");
            image.EndInit();
            ImageBehavior.SetAnimatedSource(StartupAniImage, image);
            ImageBehavior.SetAnimationSpeedRatio(StartupAniImage, animationSpeedRatio);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

            UpdateWindowChromeState();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HomePageView_Closed(object? sender, EventArgs e)
        {
            App.ExitEvt?.Invoke();
        }

        private void UpdateWindowChromeState()
        {
            bool isMaximized = WindowState == WindowState.Maximized;
            MaxIcon.Kind = isMaximized
                ? PackIconMaterialKind.WindowRestore
                : PackIconMaterialKind.WindowMaximize;
        }

        private void TryDragWindow()
        {
            try
            {
                if (WindowState == WindowState.Maximized)
                {
                    Point localPoint = Mouse.GetPosition(this);
                    Point screenPoint = PointToScreen(localPoint);
                    double widthRatio = ActualWidth > 0 ? localPoint.X / ActualWidth : 0.5;

                    WindowState = WindowState.Normal;
                    Left = screenPoint.X - (RestoreBounds.Width * widthRatio);
                    Top = Math.Max(0, screenPoint.Y - 16);
                    UpdateWindowChromeState();
                }

                DragMove();
            }
            catch
            {
            }
        }

        private void OuterBorder_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool isMaximized = WindowState == WindowState.Maximized;
            double radius = isMaximized ? 0 : 5;

            OuterBorder.CornerRadius = new CornerRadius(radius);
            OuterBorderClip.RadiusX = radius;
            OuterBorderClip.RadiusY = radius;
            OuterBorderClip.Rect = new Rect(0, 0, OuterBorder.ActualWidth, OuterBorder.ActualHeight);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                TryDragWindow();
            }
        }
    }
}
