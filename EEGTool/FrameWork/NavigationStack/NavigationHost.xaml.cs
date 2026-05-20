using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace FrameWork.NavigationStack
{
    public partial class NavigationHost : UserControl
    {
        public FrameworkElement? CurrentContent { get; private set; }

        public NavigationHost()
        {
            InitializeComponent();
            NavigationService.Instance.RegisterHost(this);
        }

        /// <summary>
        /// newContent: 前进时为“新页面”，后退时为“上一个页面（previous）”
        /// animateForward: true => 前进；false => 后退
        /// </summary>
        public async Task ShowContentAsyn(FrameworkElement newContent, bool animateForward = true)
        {
            FrontPresenter.Visibility = Visibility.Visible;
            BackPresenter.Visibility = Visibility.Visible;

            if (animateForward)
            {
                BackPresenter.Content = FrontPresenter.Content;
                BackPresenter.Visibility = BackPresenter.Content == null ? Visibility.Collapsed : Visibility.Visible;

                FrontPresenter.Content = newContent;
            }
            else
            {
                BackPresenter.Content = newContent;
                BackPresenter.Visibility = Visibility.Visible;
                // FrontPresenter.Content 保持当前页面
            }

            CurrentContent = newContent;

            await PlayFadeAnimationAsync(animateForward);
        }

        private Task PlayFadeAnimationAsync(bool forward)
        {
            var tcs = new TaskCompletionSource<bool>();
            const double animMs = 220;
            var duration = new Duration(TimeSpan.FromMilliseconds(animMs));

            FrontPresenter.BeginAnimation(UIElement.OpacityProperty, null);
            BackPresenter.BeginAnimation(UIElement.OpacityProperty, null);

            DoubleAnimation frontAnim, backAnim;

            if (forward)
            {
                FrontPresenter.Opacity = 0;
                BackPresenter.Opacity = 1;

                frontAnim = new DoubleAnimation(0, 1, duration);
                backAnim = new DoubleAnimation(1, 0, duration);
            }
            else
            {
                FrontPresenter.Opacity = 1;
                BackPresenter.Opacity = 0;

                frontAnim = new DoubleAnimation(1, 0, duration);
                backAnim = new DoubleAnimation(0, 1, duration);
            }

            Storyboard.SetTarget(frontAnim, FrontPresenter);
            Storyboard.SetTargetProperty(frontAnim, new PropertyPath("Opacity"));
            Storyboard.SetTarget(backAnim, BackPresenter);
            Storyboard.SetTargetProperty(backAnim, new PropertyPath("Opacity"));

            var sb = new Storyboard { Duration = duration };
            sb.Children.Add(frontAnim);
            sb.Children.Add(backAnim);

            sb.Completed += (s, e) =>
            {
                FrontPresenter.BeginAnimation(UIElement.OpacityProperty, null);
                BackPresenter.BeginAnimation(UIElement.OpacityProperty, null);

                if (forward)
                {
                    BackPresenter.Content = null;
                    BackPresenter.Visibility = Visibility.Collapsed;
                    FrontPresenter.Opacity = 1;
                    FrontPresenter.Visibility = Visibility.Visible;
                }
                else
                {
                    FrontPresenter.Content = BackPresenter.Content;
                    BackPresenter.Content = null;
                    BackPresenter.Visibility = Visibility.Collapsed;
                    FrontPresenter.Opacity = 1;
                    FrontPresenter.Visibility = Visibility.Visible;
                }

                tcs.SetResult(true);
            };

            sb.Begin();
            return tcs.Task;
        }
    }
}
