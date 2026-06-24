using EEGTool.Models.Collection;
using Framework.Event;
using FrameWork.Event;
using FrameWork.MVVM;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace EEGTool.ViewModels.Collection
{
    public class BandPowerMonitorViewModel : BindableBase
    {
        private static readonly BandInfo[] Bands =
        {
            new("DELTA", "0.5-4Hz", 0.5, 4, "#EF5350"),
            new("THETA", "4-8Hz", 4, 8, "#F4D03F"),
            new("ALPHA", "8-13Hz", 8, 13, "#5D8F7B"),
            new("BETA", "13-32Hz", 13, 32, "#5E78B4"),
            new("GAMMA", "32-100Hz", 32, 100, "#8E68A7")
        };

        private readonly object _dataLock = new();
        private readonly double[] _smoothedPowers = new double[Bands.Length];
        private DataProcessingResult? _latestResult;
        private long _renderVersion;

        public WpfPlot BandPowerPlot { get; } = new();
        public ObservableCollection<string> ChannelItems { get; } = new() { "Channels" };
        public ObservableCollection<string> FilterItems { get; } = new() { "Filtered" };
        public ObservableCollection<double> SmoothItems { get; } = new() { 0, 0.25, 0.5, 0.75, 0.9 };

        private string _selectedChannel = "Channels";
        public string SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                if (SetProperty(ref _selectedChannel, value))
                {
                    RenderLatest();
                }
            }
        }

        private string _selectedFilter = "Filtered";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set => SetProperty(ref _selectedFilter, value);
        }

        private double _selectedSmooth = 0.9;
        public double SelectedSmooth
        {
            get => _selectedSmooth;
            set
            {
                if (SetProperty(ref _selectedSmooth, Math.Clamp(value, 0, 0.99)))
                {
                    RenderLatest(resetSmoothing: true);
                }
            }
        }

        public BandPowerMonitorViewModel()
        {
            ConfigurePlot();

            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(
                EventName.RECEVIED_COLLECTION_DATA,
                ReceivedData);
        }

        private void ReceivedData(DataProcessingResult result)
        {
            if (result == null || result.HeadWideBandPower.Length == 0)
            {
                return;
            }

            lock (_dataLock)
            {
                _latestResult = result;
            }

            RenderLatest();
        }

        private void RenderLatest(bool resetSmoothing = false)
        {
            DataProcessingResult? result;
            long version;
            lock (_dataLock)
            {
                result = _latestResult;
                version = ++_renderVersion;
            }

            if (result == null)
            {
                return;
            }

            double[] values = BuildBandValues(result, resetSmoothing);

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (version == _renderVersion)
                {
                    DrawBars(values);
                }
            });
        }

        private double[] BuildBandValues(DataProcessingResult result, bool resetSmoothing)
        {
            int bandCount = Math.Min(Bands.Length, result.HeadWideBandPower.Length);
            var current = new double[Bands.Length];
            for (int index = 0; index < bandCount; index++)
            {
                current[index] = Math.Max(0, result.HeadWideBandPower[index]);
            }

            if (resetSmoothing || _smoothedPowers.All(value => value <= 0))
            {
                Array.Copy(current, _smoothedPowers, current.Length);
                return current;
            }

            double factor = SelectedSmooth;
            for (int index = 0; index < current.Length; index++)
            {
                _smoothedPowers[index] = _smoothedPowers[index] * factor + current[index] * (1 - factor);
            }

            return _smoothedPowers.ToArray();
        }

        private void DrawBars(double[] values)
        {
            BandPowerPlot.Plot.Remove<BarPlot>();

            double[] displayValues = values
                .Select(value => Math.Log10(Math.Max(value, 0.1)))
                .ToArray();

            BarPlot bars = BandPowerPlot.Plot.Add.Bars(displayValues);
            bars.Color = ScottPlot.Color.FromHex("#5E78B4");

            double[] tickPositions = Enumerable.Range(0, Bands.Length).Select(index => (double)index).ToArray();
            string[] tickLabels = Bands.Select(band => $"{band.Name}\n{band.Range}").ToArray();
            BandPowerPlot.Plot.Axes.Bottom.SetTicks(tickPositions, tickLabels);
            BandPowerPlot.Plot.Axes.Left.SetTicks(
                new[] { Math.Log10(0.1), Math.Log10(1), Math.Log10(10), Math.Log10(100) },
                new[] { "0.1", "1", "10", "100" });
            BandPowerPlot.Plot.Axes.SetLimitsX(-0.6, Bands.Length - 0.4);
            BandPowerPlot.Plot.Axes.SetLimitsY(Math.Log10(0.1), Math.Log10(100));
            BandPowerPlot.Refresh();
        }

        private void ConfigurePlot()
        {
            Plot plot = BandPowerPlot.Plot;
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#F4F4F4");
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#C8C8C8");
            plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#D6D6D6");
            plot.Grid.LinePattern = LinePattern.Solid;
            plot.Axes.Left.Label.Text = "Power -- (uV)^2 / Hz";
            plot.Axes.Bottom.Label.Text = "EEG Power Bands";
            plot.Benchmark.IsVisible = false;
            BandPowerPlot.UserInputProcessor.Disable();
            DrawBars(new[] { 0.1, 0.1, 0.1, 0.1, 0.1 });
        }

        private sealed record BandInfo(string Name, string Range, double LowHz, double HighHz, string Color);
    }
}
