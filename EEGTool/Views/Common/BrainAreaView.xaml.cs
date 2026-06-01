using FrameWork.Event;
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
using Windows.ApplicationModel.VoiceCommands;
using Windows.Web.Http.Diagnostics;

namespace EEGTool.Views.Common
{
    /// <summary>
    /// BrainAreaView.xaml 的交互逻辑
    /// </summary>
    public partial class BrainAreaView : UserControl
    {
        private List<Button> btns = new List<Button>();
        public static readonly DependencyProperty IsReadOnlyDependencyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(BrainAreaView),
                new PropertyMetadata(true, null));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyDependencyProperty);
            set => SetValue(IsReadOnlyDependencyProperty, value);
        }

        public class Electrode
        {
            public string Name { get; set; }
            public double X { get; set; }  // 相对坐标 0~1
            public double Y { get; set; }  // 相对坐标 0~1

            public bool IsEnable { get; set; } = true;
        }

        public List<Electrode> Electrodes = new()
        {
            // 前额区
            new() { Name="Fp1", X=0.379, Y=0.21 },
            new() { Name="Fpz", X=0.501, Y=0.20 },
            new() { Name="Fp2", X=0.6188, Y=0.21 },

            new() { Name="AF7", X=0.279, Y=0.253 },
            new() { Name="AF3", X=0.369, Y=0.2918 },
            new() { Name="AFz", X=0.502, Y=0.312 },
            new() { Name="AF4", X=0.6295, Y=0.2915 },
            new() { Name="AF8", X=0.722, Y=0.25},

            //// 额叶
            new() { Name="F7", X=0.187, Y=0.3385 },
            new() { Name="F5", X=0.266, Y=0.362 },
            new() { Name="F3", X=0.3423, Y=0.3755 },
            new() { Name="F1", X=0.4185, Y=0.3835 },
            new() { Name="Fz", X=0.5002, Y=0.392 },
            new() { Name="F2", X=0.580, Y=0.3835 },
            new() { Name="F4", X=0.655, Y=0.3755 },
            new() { Name="F6", X=0.7342, Y=0.362 },
            new() { Name="F8", X=0.812, Y=0.339 },

            //// 额中-中央前区
            new() { Name="FT9", X=0.085, Y=0.355 },
            new() { Name="FT7", X=0.145, Y=0.427 },
            new() { Name="FC5", X=0.227, Y=0.445 },
            new() { Name="FC3", X=0.315, Y=0.456 },
            new() { Name="FC1", X=0.417, Y=0.461 },
            new() { Name="FCz", X=0.50, Y=0.469 },
            new() { Name="FC2", X=0.583, Y=0.461 },
            new() { Name="FC4", X=0.682, Y=0.454 },
            new() { Name="FC6", X=0.773, Y=0.445 },
            new() { Name="FT8", X=0.852, Y=0.425 },
            new() { Name="FT10", X=0.909, Y=0.357 },

            //// 中央区
            new() { Name="T7", X=0.125, Y=0.536 },
            new() { Name="C5", X=0.221, Y=0.538 },
            new() { Name="C3", X=0.309, Y=0.538 },
            new() { Name="C1", X=0.4025, Y=0.536 },
            new() { Name="Cz", X=0.5002, Y=0.542 },
            new() { Name="C2", X=0.599, Y=0.536 },
            new() { Name="C4", X=0.695, Y=0.538 },
            new() { Name="C6", X=0.782, Y=0.536 },
            new() { Name="T8", X=0.876, Y=0.538 },

            //// 顶区
            new() { Name="FP9", X=0.047, Y=0.692 },
            new() { Name="TP7", X=0.1328, Y=0.653 },
            new() { Name="CP5", X=0.222, Y=0.622 },
            new() { Name="CP3", X=0.3162, Y=0.622 },
            new() { Name="CP1", X=0.411, Y=0.619 },
            new() { Name="CPz", X=0.5002, Y=0.612 },
            new() { Name="CP2", X=0.5942, Y=0.6185 },
            new() { Name="CP4", X=0.6846, Y=0.6228 },
            new() { Name="CP6", X=0.7718, Y=0.6365 },
            new() { Name="TP8", X=0.8616, Y=0.6539 },
            new() { Name="TP10", X=0.953, Y=0.692 },

            //// 顶叶
            new() { Name="P7", X=0.160, Y=0.748 },
            new() { Name="P5", X=0.235, Y=0.699 },
            new() { Name="P3", X=0.328, Y=0.695 },
            new() { Name="P1", X=0.411, Y=0.688 },
            new() { Name="Pz", X=0.5002, Y=0.688 },
            new() { Name="P2", X=0.5892, Y=0.688 },
            new() { Name="P4", X=0.672, Y=0.695 },
            new() { Name="P6", X=0.762, Y=0.71 },
            new() { Name="P8", X=0.836, Y=0.747 },

            //// 枕叶
            new() { Name="PO5", X=0.282, Y=0.76 },
            new() { Name="PO3", X=0.365, Y=0.775 },
            new() { Name="POz", X=0.5018, Y=0.77 },
            new() { Name="PO4", X=0.625, Y=0.778 },
            new() { Name="PO6", X=0.705, Y=0.763 },

            new() { Name="PO7", X=0.252, Y=0.821 },
            new() { Name="O1", X=0.369, Y=0.86 },
            new() { Name="Oz", X=0.502, Y=0.871 },
            new() { Name="O2", X=0.631, Y=0.858 },
            new() { Name="PO8", X=0.7319, Y=0.8235 },
        };

        public Action<string>? SelectElectrode;

        public BrainAreaView()
        {
            InitializeComponent();
        }

        private void DrawElectrodes()
        {
            EegCanvas.Children.Clear();
            double width = EegCanvas.ActualWidth;
            double height = EegCanvas.ActualHeight;
            btns.Clear();
            foreach (var e in Electrodes)
            {
                var btn = new Button
                {
                    // Content = e.Name,
                    Content = new TextBlock
                    {
                        Text = e.Name,  // 你的换行文本
                        TextAlignment = TextAlignment.Center,  // 文本水平居中
                        VerticalAlignment = VerticalAlignment.Center, // 垂直居中
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = Brushes.White
                    },
                    Style = (Style)FindResource("CircleButtonStyle"),
                    Tag = e.Name,
                    IsEnabled = e.IsEnable,
                    Background = e.IsEnable ? Brushes.LightGray : (Brush)new BrushConverter().ConvertFrom("#378CF3")
                };
                // 增加按钮大小计算公式，按钮跟随头像Canvas变大
                int btnSideLength = (int)(0.0651 * height);
                btn.Width = btnSideLength;
                btn.Height = btnSideLength;
                btns.Add(btn);
                // 按比例放置
                Canvas.SetLeft(btn, e.X * width - btn.Width / 2);
                Canvas.SetTop(btn, e.Y * height - btn.Height / 2);

                if (!IsReadOnly)
                {
                    btn.Click += Electrode_Click;
                }

                EegCanvas.Children.Add(btn);
            }
        }

        public void DeleteElectode(string eleName)
        {
            var ele = Electrodes.Find(o => o.Name.Equals(eleName));
            if(ele == null) 
                return;
            if (!ele.IsEnable)
            {
                ele.IsEnable = true;
            }
            DrawElectrodes();
        }

        public void UpdateElectrode(List<string> selectedElectrodes)
        {
            foreach (var electrode in Electrodes)
            {
                electrode.IsEnable = true;
                if (selectedElectrodes.Contains(electrode.Name))
                {
                    electrode.IsEnable = false;
                }
            }

            DrawElectrodes();
        }

        private void Electrode_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                return;
            }

            if (!btn.IsEnabled)
            {
                return;
            }

            var selectedNum = Electrodes.Count(o => !o.IsEnable);//选中的数量超过16个就不能选中了
            if (selectedNum >= 16)
            {
                return;
            }

            var eleName = btn.Tag.ToString();
            if (btn.IsEnabled)
            {
                var ele = Electrodes.Find(o => o.Name.Equals(eleName));
                if (ele == null)
                {
                    return;
                }
                ele.IsEnable = false;
                SelectElectrode?.Invoke(eleName);
                DrawElectrodes();
            }
        }

        private void EEGCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawElectrodes();
        }

 

        
    }
}
