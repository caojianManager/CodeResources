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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace EEGTool.ViewModels.Collection
{
    public class FFTMonitorViewModel : BindableBase
    {
        private readonly object _dataLock = new();
        private readonly object _smoothLock = new();
        private readonly object _preparedLock = new();
        private readonly Dictionary<int, double[]> _smoothedAmplitude = new();
        private const double DisplayEasingFactor = 0.22;
        private readonly DispatcherTimer _renderTimer = new(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        private DataProcessingResult? _latestResult;
        private List<FftSeries>? _preparedSeries;
        private List<FftSeries>? _displaySeries;
        private int _preparedSampleRate;
        private int _cachedChannelNameCount = -1;
        private List<string> _cachedChannelNames = new();
        private long _latestDataVersion;
        private long _preparedFrameVersion;
        private long _displayTargetFrameVersion;
        private bool _displaySettled = true;
        private int _isPreparing;

        public WpfPlot FftPlot { get; } = new();
        public ObservableCollection<int> MaxWindowHz { get; } = new() { 30, 60, 100, 125 };
        public ObservableCollection<int> MaxWindowUv { get; } = new() { 10, 25, 50, 100, 200, 500 };
        public ObservableCollection<string> YAxesType { get; } = new() { "Lin", "Log" };
        public ObservableCollection<double> SmoothingFactor { get; } = new() { 0, 0.25, 0.5, 0.75, 0.9 };

        private int _selectedMaxWindowHz = 60;
        public int SelectedMaxWindowHz
        {
            get => _selectedMaxWindowHz;
            set { if (SetProperty(ref _selectedMaxWindowHz, value)) QueuePrepareLatest(); }
        }

        private int _selectedMaxWindowUv = 100;
        public int SelectedMaxWindowUv
        {
            get => _selectedMaxWindowUv;
            set { if (SetProperty(ref _selectedMaxWindowUv, value)) ForceRenderPrepared(); }
        }

        private string _selectedYAxesType = "Lin";
        public string SelectedYAxesType
        {
            get => _selectedYAxesType;
            set { if (SetProperty(ref _selectedYAxesType, value)) QueuePrepareLatest(); }
        }

        private double _selectedSmoothingFactor = 0.9;
        public double SelectedSmoothingFactor
        {
            get => _selectedSmoothingFactor;
            set
            {
                if (SetProperty(ref _selectedSmoothingFactor, Math.Clamp(value, 0, 0.99)))
                {
                    lock (_smoothLock)
                    {
                        _smoothedAmplitude.Clear();
                    }

                    QueuePrepareLatest();
                }
            }
        }

        public FFTMonitorViewModel()
        {
            ConfigurePlot();
            _renderTimer.Tick += (_, _) => RenderPrepared();
            _renderTimer.Start();
            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(
                EventName.RECEVIED_COLLECTION_DATA,
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
                _latestDataVersion++;
            }

            QueuePrepareLatest();
        }

        private void QueuePrepareLatest()
        {
            if (Interlocked.Exchange(ref _isPreparing, 1) == 1)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                long preparedVersion = 0;
                try
                {
                    while (true)
                    {
                        DataProcessingResult? result;
                        long version;
                        lock (_dataLock)
                        {
                            result = _latestResult;
                            version = _latestDataVersion;
                        }

                        if (result == null || version == preparedVersion)
                        {
                            break;
                        }

                        int sampleRate = Math.Max(1, CollectionInfoManager.GetInstance().Info.SampleRate);
                        List<FftSeries> series;
                        lock (_smoothLock)
                        {
                            series = BuildSeries(result, sampleRate);
                        }

                        lock (_preparedLock)
                        {
                            _preparedSeries = series;
                            _preparedSampleRate = sampleRate;
                            _preparedFrameVersion++;
                        }

                        preparedVersion = version;

                        lock (_dataLock)
                        {
                            if (_latestDataVersion == version)
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _isPreparing, 0);

                    lock (_dataLock)
                    {
                        if (_latestResult != null && _latestDataVersion != preparedVersion)
                        {
                            QueuePrepareLatest();
                        }
                    }
                }
            });
        }

        private void ForceRenderPrepared()
        {
            lock (_preparedLock)
            {
                _preparedFrameVersion++;
            }

            RenderPrepared();
        }

        private void RenderPrepared()
        {
            List<FftSeries> targetSeries;
            int sampleRate;
            long frameVersion;
            lock (_preparedLock)
            {
                if (_preparedSeries == null)
                {
                    return;
                }

                targetSeries = _preparedSeries;
                sampleRate = _preparedSampleRate;
                frameVersion = _preparedFrameVersion;
            }

            if (frameVersion != _displayTargetFrameVersion)
            {
                _displayTargetFrameVersion = frameVersion;
                _displaySettled = false;
            }

            if (_displaySettled)
            {
                return;
            }

            List<FftSeries> displaySeries = BuildDisplaySeries(targetSeries);
            DrawSeries(displaySeries, sampleRate);
        }

        private List<FftSeries> BuildDisplaySeries(List<FftSeries> targetSeries)
        {
            if (_displaySeries == null || !HasSameShape(_displaySeries, targetSeries))
            {
                _displaySeries = targetSeries
                    .Select(item => new FftSeries(
                        item.Channel,
                        item.Frequencies.ToArray(),
                        item.Amplitude.ToArray()))
                    .ToList();
                _displaySettled = false;
                return _displaySeries;
            }

            double maxDelta = 0;
            for (int seriesIndex = 0; seriesIndex < targetSeries.Count; seriesIndex++)
            {
                double[] display = _displaySeries[seriesIndex].Amplitude;
                double[] target = targetSeries[seriesIndex].Amplitude;
                for (int index = 0; index < display.Length; index++)
                {
                    double delta = target[index] - display[index];
                    display[index] += delta * DisplayEasingFactor;
                    maxDelta = Math.Max(maxDelta, Math.Abs(delta));
                }
            }

            if (maxDelta < 0.01)
            {
                for (int seriesIndex = 0; seriesIndex < targetSeries.Count; seriesIndex++)
                {
                    Array.Copy(targetSeries[seriesIndex].Amplitude, _displaySeries[seriesIndex].Amplitude, targetSeries[seriesIndex].Amplitude.Length);
                }

                _displaySettled = true;
            }

            return _displaySeries;
        }

        private static bool HasSameShape(List<FftSeries> displaySeries, List<FftSeries> targetSeries)
        {
            if (displaySeries.Count != targetSeries.Count)
            {
                return false;
            }

            for (int index = 0; index < targetSeries.Count; index++)
            {
                if (displaySeries[index].Channel != targetSeries[index].Channel ||
                    displaySeries[index].Frequencies.Length != targetSeries[index].Frequencies.Length ||
                    displaySeries[index].Amplitude.Length != targetSeries[index].Amplitude.Length)
                {
                    return false;
                }
            }

            return true;
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
                    ? smoothed.Select(ToLogAxisValue).ToArray()
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
            List<string> channelNames = GetCachedChannelNames(series.Count);

            foreach (FftSeries item in series)
            {
                Scatter scatter = FftPlot.Plot.Add.Scatter(item.Frequencies, item.Amplitude);
                scatter.LineWidth = 1.5f;
                scatter.MarkerSize = 0;
                scatter.Smooth = true;
                scatter.SmoothTension = 0.35f;
                scatter.Color = Constants.ChannelColors[item.Channel % Constants.ChannelColors.Length];
                scatter.LegendText = item.Channel < channelNames.Count
                    ? channelNames[item.Channel]
                    : $"Ch{item.Channel + 1}";
            }

            FftPlot.Plot.Axes.SetLimitsX(0, Math.Max(1, Math.Min(SelectedMaxWindowHz, sampleRate / 2.0)));
            if (SelectedYAxesType == "Log")
            {
                FftPlot.Plot.Axes.SetLimitsY(0, ToLogAxisValue(SelectedMaxWindowUv));
                SetLogYAxisTicks(SelectedMaxWindowUv);
            }
            else
            {
                FftPlot.Plot.Axes.SetLimitsY(0, SelectedMaxWindowUv);
                SetLinearYAxisTicks(SelectedMaxWindowUv);
            }

            FftPlot.Refresh();
        }

        private List<string> GetCachedChannelNames(int channelCount)
        {
            if (_cachedChannelNameCount != channelCount)
            {
                _cachedChannelNames = GetChannelNames(channelCount);
                _cachedChannelNameCount = channelCount;
            }

            return _cachedChannelNames;
        }

        private void SetLogYAxisTicks(int maxUv)
        {
            double[] candidates = { 0, 1, 2, 5, 10, 20, 50, 100, 200, 500 };
            double[] ticks = candidates
                .Where(value => value <= maxUv)
                .Select(ToLogAxisValue)
                .ToArray();
            string[] labels = candidates
                .Where(value => value <= maxUv)
                .Select(value => value.ToString("0"))
                .ToArray();

            FftPlot.Plot.Axes.Left.SetTicks(ticks, labels);
        }

        private void SetLinearYAxisTicks(int maxUv)
        {
            int step = maxUv switch
            {
                <= 10 => 2,
                <= 25 => 5,
                <= 50 => 10,
                <= 100 => 20,
                <= 200 => 50,
                _ => 100
            };

            double[] ticks = Enumerable.Range(0, maxUv / step + 1)
                .Select(index => (double)(index * step))
                .Where(value => value <= maxUv)
                .ToArray();
            string[] labels = ticks.Select(value => value.ToString("0")).ToArray();
            FftPlot.Plot.Axes.Left.SetTicks(ticks, labels);
        }

        private static double ToLogAxisValue(double value)
        {
            return Math.Log10(Math.Max(0, value) + 1);
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
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#B0BEC5");
            plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#B0BEC5");
            plot.Grid.LinePattern = LinePattern.DenselyDashed;

            plot.Axes.Bottom.Label.Text = "Frequency (Hz)";
            plot.Axes.Left.Label.Text = "Amplitude (μV)";
            plot.Benchmark.IsVisible = false;
            plot.ShowLegend(Alignment.UpperRight);
            FftPlot.UserInputProcessor.Disable();
        }

        private sealed record FftSeries(int Channel, double[] Frequencies, double[] Amplitude);
    }
}
