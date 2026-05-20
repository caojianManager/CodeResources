using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FrameWork.View
{
    public partial class BaseWindow : Window
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_GETMINMAXINFO = 0x0024;

        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int MONITOR_DEFAULTTONEAREST = 2;
        private const int ResizeBorderThickness = 8;

        private Grid? _animationWrapper;
        private bool _isAnimationFinished = false;

        public BaseWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            Loaded += BaseWindow_Loaded;
            SourceInitialized += BaseWindow_SourceInitialized;
        }

        private void BaseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(WrapContentAndAnimate),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void BaseWindow_SourceInitialized(object? sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
        }

        private void WrapContentAndAnimate()
        {
            if (_animationWrapper != null)
                return;

            object? originalContent = Content;
            Content = null;

            if (originalContent == null)
                originalContent = new Grid();

            _animationWrapper = new Grid
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = 0
            };

            if (originalContent is UIElement element)
                _animationWrapper.Children.Add(element);

            Content = _animationWrapper;

            var duration = TimeSpan.FromMilliseconds(300);
            var opacityAnim = new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            _animationWrapper.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            opacityAnim.Completed += (s, e) => { _isAnimationFinished = true; };
        }

        private void AnimationWrapper_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsActive)
                Activate();

            if (e.ClickCount == 2)
            {
                if (_isAnimationFinished)
                    ToggleMaximize();
            }
            else if (e.ButtonState == MouseButtonState.Pressed && WindowState != WindowState.Maximized)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                }
            }
        }

        public void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_NCHITTEST:
                    return HandleNcHitTest(lParam, ref handled);
                case WM_GETMINMAXINFO:
                    UpdateMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        private IntPtr HandleNcHitTest(IntPtr lParam, ref bool handled)
        {
            if (ResizeMode != ResizeMode.CanResize && ResizeMode != ResizeMode.CanResizeWithGrip)
                return IntPtr.Zero;

            if (WindowState == WindowState.Maximized)
                return IntPtr.Zero;

            Point screenPoint = GetPointFromLParam(lParam);
            Point windowPoint = PointFromScreen(screenPoint);

            double width = ActualWidth;
            double height = ActualHeight;

            bool onLeft = windowPoint.X >= 0 && windowPoint.X <= ResizeBorderThickness;
            bool onRight = windowPoint.X <= width && windowPoint.X >= width - ResizeBorderThickness;
            bool onTop = windowPoint.Y >= 0 && windowPoint.Y <= ResizeBorderThickness;
            bool onBottom = windowPoint.Y <= height && windowPoint.Y >= height - ResizeBorderThickness;

            if (onLeft && onTop)
            {
                handled = true;
                return new IntPtr(HTTOPLEFT);
            }

            if (onRight && onTop)
            {
                handled = true;
                return new IntPtr(HTTOPRIGHT);
            }

            if (onLeft && onBottom)
            {
                handled = true;
                return new IntPtr(HTBOTTOMLEFT);
            }

            if (onRight && onBottom)
            {
                handled = true;
                return new IntPtr(HTBOTTOMRIGHT);
            }

            if (onLeft)
            {
                handled = true;
                return new IntPtr(HTLEFT);
            }

            if (onRight)
            {
                handled = true;
                return new IntPtr(HTRIGHT);
            }

            if (onTop)
            {
                handled = true;
                return new IntPtr(HTTOP);
            }

            if (onBottom)
            {
                handled = true;
                return new IntPtr(HTBOTTOM);
            }

            return new IntPtr(HTCLIENT);
        }

        private static Point GetPointFromLParam(IntPtr lParam)
        {
            int x = unchecked((short)(long)lParam);
            int y = unchecked((short)((long)lParam >> 16));
            return new Point(x, y);
        }

        private static void UpdateMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MinMaxInfo mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                MonitorInfo monitorInfo = new MonitorInfo
                {
                    cbSize = Marshal.SizeOf<MonitorInfo>()
                };

                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    Rect workArea = monitorInfo.rcWork.ToRect();
                    Rect monitorArea = monitorInfo.rcMonitor.ToRect();

                    mmi.ptMaxPosition.X = (int)(workArea.Left - monitorArea.Left);
                    mmi.ptMaxPosition.Y = (int)(workArea.Top - monitorArea.Top);
                    mmi.ptMaxSize.X = (int)workArea.Width;
                    mmi.ptMaxSize.Y = (int)workArea.Height;
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct PointInt
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MinMaxInfo
        {
            public PointInt ptReserved;
            public PointInt ptMaxSize;
            public PointInt ptMaxPosition;
            public PointInt ptMinTrackSize;
            public PointInt ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int cbSize;
            public RectInt rcMonitor;
            public RectInt rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RectInt
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rect ToRect()
            {
                return new Rect(Left, Top, Right - Left, Bottom - Top);
            }
        }
    }
}
