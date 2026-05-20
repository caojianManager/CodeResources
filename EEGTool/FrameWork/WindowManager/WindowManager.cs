using System.Windows;
using System.Windows.Media.Animation;
using FrameWork;
using FrameWork.Common;

namespace Framework
{
    public class WindowManager : Singleton<WindowManager>, IWindowManager
    {
        public async Task SwitchWindowAsync(object oldViewModel, object newViewModel)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // 1️⃣ 创建并淡入新窗口
                var newWindow = CreateWindow(newViewModel);
                await FadeInAsync(newWindow);

                // 2️⃣ 等新窗口完全显示后，淡出旧窗口
                if (FindWindow(oldViewModel) is Window oldWindow)
                    await FadeOutAndCloseAsync(oldWindow);
            });
        }

        public void ShowWindow(object viewModel)
        {
            var window = CreateWindow(viewModel);
            window.Show();
            window.Activate();
        }

        public void CloseWindow(object viewModel)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (FindWindow(viewModel) is Window window)
                    window.Close();
            });
        }

        public async Task CloseWindowAsyn(object viewModel)
        {
            await Task.Delay(500);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (FindWindow(viewModel) is Window window)
                    window.Close();
            });
        }

        public void ActivateWindow(object viewModel)
        {
            if (FindWindow(viewModel) is Window window && window.IsVisible)
                window.Activate();
        }

        public void ShowDialog(object viewModel)
        {
            var window = CreateWindow(viewModel);
            window.ShowDialog();
        }

        public async Task ShowWindowAsync(object viewModel)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var window = CreateWindow(viewModel);
                window.Show();
                window.Activate();
            });
        }

        private Window CreateWindow(object viewModel)
        {
            var view = ViewLocator.LocateForModel(viewModel);
            if (view is not Window window)
                throw new InvalidOperationException("ViewLocator must return a Window.");

            window.DataContext = viewModel;

            window.Loaded += (s, e) =>
            {
                if (viewModel is IWindowShow iShow)
                    iShow.OnWindowShow();
            };

            window.Closed += (s, e) =>
            {
                if (viewModel is IWindowClose iClose)
                    iClose.OnWindowClose();

                ViewLocator.ClearCache(viewModel);
            };

            return window;
        }

        private Window? FindWindow(object viewModel)
        {
            Window? result = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (w.DataContext == viewModel)
                    {
                        result = w;
                        return;
                    }
                }
            });

            return result;
        }

        // ===========================
        // ✨ 动画部分
        // ===========================

        private async Task FadeInAsync(Window window)
        {
            window.Opacity = 0;
            window.Show();
            window.Activate();

            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            window.BeginAnimation(Window.OpacityProperty, anim);

            await Task.Delay(anim.Duration.TimeSpan);
        }

        private async Task FadeOutAndCloseAsync(Window window)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            window.BeginAnimation(Window.OpacityProperty, anim);

            await Task.Delay(anim.Duration.TimeSpan);
            window.Close();
        }
    }
}
