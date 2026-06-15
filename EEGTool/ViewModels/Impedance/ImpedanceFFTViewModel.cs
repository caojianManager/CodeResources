using EEGTool.Models.Collection;
using EEGTool.Models.Template;
using Framework.Event;
using FrameWork.Common;
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

namespace EEGTool.ViewModels.Impedance
{
    public class ImpedanceFFTViewModel : BindableBase
    {
        private readonly object _dataLock = new();
        private readonly Dictionary<int, double[]> _smoothedAmplitude = new();
        private DataProcessingResult? _latestResult;
        private long _renderVersion;

        public WpfPlot FftPlot { get; } = new();
        public ObservableCollection<int> MaxWindowHz { get; } = new() { 30, 60, 100, 125 };
        public ObservableCollection<int> MaxWindowUv { get; } = new() { 10, 25, 50, 100, 200, 500 };
        public ObservableCollection<string> YAxesType { get; } = new() { "Lin", "Log" };
        public ObservableCollection<double> SmoothingFactor { get; } = new() { 0, 0.25, 0.5, 0.75, 0.9 };

        private int _selectedMaxWindowHz = 60;
        public int SelectedMaxWindowHz
        {
            get => _selectedMaxWindowHz;
            set { if (SetProperty(ref _selectedMaxWindowHz, value)) RenderLatest(); }
        }

        private int _selectedMaxWindowUv = 100;
        public int SelectedMaxWindowUv
        {
            get => _selectedMaxWindowUv;
            set { if (SetProperty(ref _selectedMaxWindowUv, value)) RenderLatest(); }
        }

        private string _selectedYAxesType = "Lin";
        public string SelectedYAxesType
        {
            get => _selectedYAxesType;
            set { if (SetProperty(ref _selectedYAxesType, value)) RenderLatest(); }
        }

        private double _selectedSmoothingFactor = 0.5;
        public double SelectedSmoothingFactor
        {
            get => _selectedSmoothingFactor;
            set
            {
                if (SetProperty(ref _selectedSmoothingFactor, Math.Clamp(value, 0, 0.99)))
                {
                    _smoothedAmplitude.Clear();
                    RenderLatest();
                }
            }
        }

        public ImpedanceFFTViewModel()
        {
            ConfigurePlot();
            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(
                EventName.RECEVIED_IMPEDANCE_DATA,
                ReceivedData);
        }

        private void ReceivedData(DataProcessingResult result)
        {
            if (result == null ||
                !result.FftAmplitudeByChannel.Any(channel => channel != null && channel.Length > 0))
            {
                return;
            }

            lock (_dataLock)
            {
                _latestResult = result;
            }

            RenderLatest();
        }

        private void RenderLatest()
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

            int sampleRate = Math.Max(1, CollectionInfoManager.GetInstance().Info.SampleRate);
            List<FftSeries> series = BuildSeries(result, sampleRate);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (version == _renderVersion)
                {
                    DrawSeries(series, sampleRate);
                }
            });
        }

        private List<FftSeries> BuildSeries(DataProcessingResult result, int sampleRate)
        {
            var series = new List<FftSeries>();
            for (int channel = 0; channel < result.FftAmplitudeByChannel.Length; channel++)
            {
                float[] amplitude = result.FftAmplitudeByChannel[channel] ?? Array.Empty<float>();
                if (amplitude.Length <= 1)
                {
                    continue;
                }

                int nfft = Math.Max(2, (amplitude.Length - 1) * 2);
                double binWidth = (double)sampleRate / nfft;
                int maxBin = Math.Min(
                    amplitude.Length - 1,
                    (int)Math.Floor(Math.Min(SelectedMaxWindowHz, sampleRate / 2.0) / binWidth));
                if (maxBin < 1)
                {
                    continue;
                }

                var frequencies = new double[maxBin];
                var current = new double[maxBin];
                for (int index = 1; index <= maxBin; index++)
                {
                    frequencies[index - 1] = index * binWidth;
                    current[index - 1] = amplitude[index];
                }

                double[] smoothed = Smooth(channel, current);
                double[] display = SelectedYAxesType == "Log"
                    ? smoothed.Select(value => 20 * Math.Log10(Math.Max(value, 1e-4))).ToArray()
                    : smoothed;
                series.Add(new FftSeries(channel, frequencies, display));
            }

            return series;
        }

        private double[] Smooth(int channel, double[] current)
        {
            double factor = SelectedSmoothingFactor;
            if (factor <= 0 ||
                !_smoothedAmplitude.TryGetValue(channel, out double[]? previous) ||
                previous.Length != current.Length)
            {
                double[] initial = current.ToArray();
                _smoothedAmplitude[channel] = initial;
                return initial;
            }

            var smoothed = new double[current.Length];
            for (int index = 0; index < current.Length; index++)
            {
                smoothed[index] = previous[index] * factor + current[index] * (1 - factor);
            }

            _smoothedAmplitude[channel] = smoothed;
            return smoothed;
        }

        private void DrawSeries(List<FftSeries> series, int sampleRate)
        {
            FftPlot.Plot.Remove<Scatter>();
            List<string> channelNames = GetChannelNames(series.Count);

            foreach (FftSeries item in series)
            {
                Scatter scatter = FftPlot.Plot.Add.Scatter(item.Frequencies, item.Amplitude);
                scatter.LineWidth = 1.5f;
                scatter.MarkerSize = 0;
                scatter.Color = Constants.ChannelColors[item.Channel % Constants.ChannelColors.Length];
                scatter.LegendText = item.Channel < channelNames.Count
                    ? channelNames[item.Channel]
                    : $"Ch{item.Channel + 1}";
            }

            FftPlot.Plot.Axes.SetLimitsX(0, Math.Max(1, Math.Min(SelectedMaxWindowHz, sampleRate / 2.0)));
            if (SelectedYAxesType == "Log")
            {
                FftPlot.Plot.Axes.SetLimitsY(-40, Math.Max(0, 20 * Math.Log10(Math.Max(1, SelectedMaxWindowUv))));
            }
            else
            {
                FftPlot.Plot.Axes.SetLimitsY(0, SelectedMaxWindowUv);
            }

            FftPlot.Plot.ShowLegend(Alignment.UpperRight);
            FftPlot.Refresh();
        }

        private static List<string> GetChannelNames(int channelCount)
        {
            var template = CollectionInfoManager.GetInstance().Info.Template;
            List<int> enabledChannels = TemplateFileManager.GetInstance()
                .GetCurrentChannelList(template)
                .Where(channel => channel >= 1 && channel <= EEGTool.Models.BLE.CommandManager.ChannelCount)
                .Distinct()
                .OrderBy(channel => channel)
                .ToList();
            if (enabledChannels.Count != channelCount)
            {
                enabledChannels = Enumerable.Range(1, channelCount).ToList();
            }

            return enabledChannels.Select(channel => $"Ch{channel}").ToList();
        }

        private void ConfigurePlot()
        {
            Plot plot = FftPlot.Plot;
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#111820");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#111820");
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#33424F");
            plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#22303B");
            plot.Axes.Color(ScottPlot.Color.FromHex("#B0BEC5"));
            plot.Axes.Bottom.Label.Text = "Frequency (Hz)";
            plot.Axes.Left.Label.Text = "Amplitude (µV)";
            plot.Benchmark.IsVisible = false;
            FftPlot.UserInputProcessor.Disable();
        }

        private sealed record FftSeries(int Channel, double[] Frequencies, double[] Amplitude);
    }
}
