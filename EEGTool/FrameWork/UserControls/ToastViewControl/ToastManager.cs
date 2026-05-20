using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows;
using FrameWork.Log;

namespace FrameWork.UserControls.ToastViewControl
{
    public static class ToastManager
    {
        private static readonly Dictionary<FrameworkElement, ToastView> _toasts = new();

        /// <summary>
        /// 在当前活动窗口展示 Toast 提示
        /// </summary>
        public static void Show(string message, int durationSeconds = 3)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var currentWindow = Application.Current.Windows
               .OfType<Window>()
               .FirstOrDefault(w => w.IsActive);

                if (currentWindow?.Content is FrameworkElement root)
                {
                    ShowInternal(root, message, durationSeconds);
                }
            });
        }

        public static void Show(FrameworkElement targetElement, string message, int durationSeconds = 3)
        {
            if (targetElement == null) 
                return;
            ShowInternal(targetElement, message, durationSeconds);
        }

        private static void ShowInternal(FrameworkElement target, string message, int durationSeconds)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ShowInternal(target, message, durationSeconds));
                return;
            }

            if (!_toasts.TryGetValue(target, out var toast))
            {
                toast = new ToastView();
                toast.SetMessage(message);
                toast.Visibility = Visibility.Visible;

                if (target is Grid grid)
                {
                    Grid.SetRowSpan(toast, int.MaxValue);
                    Grid.SetColumnSpan(toast, int.MaxValue);
                    grid.Children.Add(toast);
                }
                else if (target is Panel panel)
                {
                    panel.Children.Add(toast);
                }
                else if (target is ContentControl contentControl)
                {
                    var originalContent = contentControl.Content as UIElement;
                    var wrapper = new Grid { Tag = "__ToastWrapper" };

                    if (originalContent != null)
                    {
                        contentControl.Content = null;
                        wrapper.Children.Add(originalContent);
                    }

                    wrapper.Children.Add(toast);
                    contentControl.Content = wrapper;
                }
                else
                {
                    Logger.Info("不支持在该控件上添加Toast提示。");
                }

                _toasts[target] = toast;
                target.Unloaded += (s, e) => _toasts.Remove(target);
            }
            else
            {
                toast.SetMessage(message);
                toast.Visibility = Visibility.Visible;
            }

            // 动画：淡入
            toast.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 延迟隐藏
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (s2, e2) => toast.Visibility = Visibility.Collapsed;
                toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        }
    }
}
