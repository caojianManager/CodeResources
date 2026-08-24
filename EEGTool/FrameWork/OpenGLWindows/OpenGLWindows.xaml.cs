using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Wpf;
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

namespace FrameWork.OpenGLWindows
{
    /// <summary>
    /// OpenGLWindows.xaml 的交互逻辑
    /// </summary>
    public partial class OpenGLWindows : UserControl
    {
        public static readonly DependencyProperty RenderProperty =
            DependencyProperty.Register(
                nameof(RenderAction),
                typeof(Action<TimeSpan>),
                typeof(OpenGLWindows),
                new PropertyMetadata(null));

        public Action<TimeSpan> RenderAction
        {
            get { return (Action<TimeSpan>)GetValue(RenderProperty); }
            set { SetValue(RenderProperty, value); }
        }

        public static readonly DependencyProperty InitProperty =
            DependencyProperty.Register(
                nameof(Init),
                typeof(Action),
                typeof(OpenGLWindows),
                new PropertyMetadata(null));

        public Action Init
        {
            get { return (Action)GetValue(InitProperty); }
            set { SetValue(InitProperty, value); }
        }

        public OpenGLWindows()
        {
            InitializeComponent();
            ConfigGL();
            this.Loaded += OpenGLWindows_Loaded;
        }

        private void OpenGLWindows_Loaded(object sender, RoutedEventArgs e)
        {
            Init?.Invoke();
            OpenTkControl.Render += OpenTkControl_OnRender;
        }

        private void ConfigGL()
        {
            var settings = new GLWpfControlSettings
            {
                MajorVersion = 3,
                MinorVersion = 3
            };
            OpenTkControl.Start(settings);
        }

        private void OpenTkControl_OnRender(TimeSpan span)
        {
            RenderAction?.Invoke(span);
        }
    }
}
