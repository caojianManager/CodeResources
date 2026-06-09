using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Event;
using FrameWork.MVVM;
using ScottPlot;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EEGTool.ViewModels.Collection
{
    public class EEGMonitorViewModel : BindableBase
    {
        public WpfPlot EegPlot { get; } = new();

        private bool _isAuto = false;
        public bool IsAuto
        {
            get => _isAuto;
            set => SetProperty(ref _isAuto, value);
        }

        public ICommand? EegAutoClickCommand { get; set; }

        public EEGMonitorViewModel()
        {
            Config();
        }

        private void Config()
        {
            ConfigPlot();
            EegAutoClickCommand = new RelayCommand((o) =>
            {
                IsAuto = !IsAuto;
            });

            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(EventName.RECEVIED_COLLECTION_DATA,
                (o) => { ReceivedCollectionData(o);});
        }

        private void ReceivedCollectionData(DataProcessingResult result)
        {

        }

        /// <summary>
        /// 配置图表基础配置
        /// </summary>
        private void ConfigPlot()
        {
            var plot = EegPlot.Plot;

            ScottPlot.AxisRules.LockedHorizontal rule =
                new(plot.Axes.Bottom, 0, 1000);

            plot.Axes.Rules.Clear();
            plot.Axes.Rules.Add(rule);
            plot.Axes.AutoScale(false);
            //隐藏小刻度
            plot.Grid.MajorLineWidth = 0;

            //改成虚线
            plot.Axes.Right.FrameLineStyle.Color = ScottPlot.Color.FromHex("#E3E3E3");
            plot.Axes.Right.FrameLineStyle.Width = 1;
            plot.Axes.Right.FrameLineStyle.Pattern = LinePattern.DenselyDashed;
            plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Color.FromHex("#E3E3E3");
            plot.Axes.Bottom.FrameLineStyle.Width = 1;
            plot.Axes.Bottom.FrameLineStyle.Pattern = LinePattern.DenselyDashed;
            plot.Axes.Left.FrameLineStyle.Color = ScottPlot.Color.FromHex("#E3E3E3");
            plot.Axes.Left.FrameLineStyle.Width = 1;
            plot.Axes.Left.FrameLineStyle.Pattern = LinePattern.DenselyDashed;
            plot.Axes.Top.FrameLineStyle.Color = ScottPlot.Color.FromHex("#E3E3E3");
            plot.Axes.Top.FrameLineStyle.Width = 1;
            plot.Axes.Top.FrameLineStyle.Pattern = LinePattern.DenselyDashed;

            //隐藏左刻度-底下刻度
            plot.Axes.Left.TickLabelStyle.IsVisible = false;
            plot.Axes.Left.TickLabelStyle.FontSize = 0;
            plot.Axes.Left.MajorTickStyle.Length = 0;
            plot.Axes.Left.MinorTickStyle.Length = 0;
            plot.Axes.Bottom.MajorTickStyle.Length = 0;
            plot.Axes.Bottom.MinorTickStyle.Length = 0;

            //禁止显示FPS
            plot.Benchmark.IsVisible = false;
            plot.RenderManager.RenderActions.RemoveAll(x => x.GetType().Name.Contains("Benchmark"));
        }
    }
}
