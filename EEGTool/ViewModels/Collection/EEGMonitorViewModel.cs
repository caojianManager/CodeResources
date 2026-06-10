using EEGTool.Models.Collection;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Common;
using FrameWork.Event;
using FrameWork.MVVM;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace EEGTool.ViewModels.Collection
{
    public class EEGMonitorViewModel : BindableBase
    {
        private enum StreamerViewMode
        {
            Scroll,
            Wipe
        }

        private const double ChannelHeight = 100;
        private const double ChannelCenterOffset = 50;
        private const double WipeBlankFraction = 0.000001;
        private readonly object _onDataLock = new();
        private readonly Dictionary<int, DataStreamer> _streamers = new();
        private readonly Dictionary<int, int> _autoValues = new();
        private readonly Queue<double[]> _pendingPlotSamples = new();
        private readonly DispatcherTimer _addNewDataTimer = new() { Interval = TimeSpan.FromMilliseconds(10) };
        private readonly DispatcherTimer _updatePlotTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
        private float[][] _currentFrameData = Array.Empty<float[]>();
        private long _latestWindowUpdateVersion;
        private DateTime _lastPlotDataTime = DateTime.MinValue;
        private int _sampleRate = 250;
        private int _lastAxisSampleRate;
        private int _lastAxisWindowSec;
        private int _lastAxisChannelCount;
        private StreamerViewMode _streamerViewMode = StreamerViewMode.Scroll;
        private VerticalLine? _wipeLine;

        public WpfPlot EegPlot { get; } = new();
        public ObservableCollection<WaveHeaderItem> WaveHeaderItems { get; } = new();

        private bool _isAuto = false;
        public bool IsAuto
        {
            get => _isAuto;
            set => SetProperty(ref _isAuto, value);
        }

        private int _windowSec = 5;
        public int WindowSec
        {
            get => _windowSec;
            set => SetProperty(ref _windowSec, Math.Max(1, value));
        }

        private int _vertScale = 100;
        public int VertScale
        {
            get => _vertScale;
            set => SetProperty(ref _vertScale, Math.Max(1, value));
        }

        public ICommand? EegAutoClickCommand { get; set; }
        public ICommand? ClickWaveHeaderCommand { get; set; }

        public EEGMonitorViewModel()
        {
            Config();

            SetStreamerViewMode(StreamerViewMode.Wipe);
        }

        private void Config()
        {
            ConfigPlot();
            _addNewDataTimer.Tick += (_, _) => AddPendingPlotSamples();
            _updatePlotTimer.Tick += (_, _) => RefreshStreamPlot();
            _addNewDataTimer.Start();
            _updatePlotTimer.Start();

            EegAutoClickCommand = new RelayCommand((o) =>
            {
                IsAuto = !IsAuto;
            });
            ClickWaveHeaderCommand = new RelayCommand((o) =>
            {
                if (o is WaveHeaderItem item)
                {
                    item.IsSelected = !item.IsSelected;
                }
            });

            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(EventName.RECEVIED_COLLECTION_DATA,
                (o) => { ReceivedCollectionData(o); });
        }

        private void ReceivedCollectionData(DataProcessingResult result)
        {
            if (result == null)
            {
                return;
            }

            float[][] dataCopy;
            long updateVersion;

            lock (_onDataLock)
            {
                updateVersion = ++_latestWindowUpdateVersion;
                dataCopy = CopyData(result.DisplayWindowByChannel);
                if (dataCopy.Length == 0)
                {
                    dataCopy = CopyData(result.FilteredByChannel);
                }

                _currentFrameData = dataCopy;
                _sampleRate = Math.Max(1, CollectionInfoManager.GetInstance().Info.SampleRate);
            }

            var renderData = BuildPlotData(dataCopy);

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (updateVersion < _latestWindowUpdateVersion)
                {
                    return;
                }

                //EnsureWaveHeaderItems(dataCopy.Length);
                QueuePlotData(renderData);
            });
        }

        private static float[][] CopyData(float[][] data)
        {
            if (data == null)
            {
                return Array.Empty<float[]>();
            }

            return data
                .Where(channelData => channelData != null && channelData.Length > 0)
                .Select(channelData => channelData.ToArray())
                .ToArray();
        }

        private List<(double[] xs, double[] ys, int ch)> BuildPlotData(float[][] data)
        {
            var result = new List<(double[], double[], int)>();
            if (data == null || data.Length == 0)
            {
                return result;
            }

            int sampleRate = Math.Max(1, _sampleRate);
            int maxCount = Math.Max(1, WindowSec * sampleRate);

            for (int ch = 0; ch < data.Length; ch++)
            {
                float[] eegData = TakeLast(data[ch], maxCount);
                if (eegData.Length == 0)
                {
                    continue;
                }

                double centerY = ch * ChannelHeight + ChannelCenterOffset;
                double mean = eegData.Average();
                double[] centered = eegData.Select(v => (double)v - mean).ToArray();
                double[] xs = Enumerable.Range(0, centered.Length).Select(i => (double)i).ToArray();
                double[] ys;

                if (IsAuto)
                {
                    double maxAbs = centered.Max(v => Math.Abs(v));
                    if (maxAbs < 1e-3)
                    {
                        maxAbs = 1;
                    }

                    double scale = (ChannelHeight / 2.0) / maxAbs;
                    ys = centered.Select(v => centerY - v * scale).ToArray();
                    _autoValues[ch] = (int)Math.Ceiling(maxAbs);
                }
                else
                {
                    double uvToY = (ChannelHeight / 2.0) / VertScale;
                    ys = centered.Select(v => centerY - v * uvToY).ToArray();
                }

                result.Add((xs, ys, ch));
            }

            return result;
        }

        private static float[] TakeLast(float[] data, int count)
        {
            if (data == null || data.Length == 0 || count <= 0)
            {
                return Array.Empty<float>();
            }

            int actual = Math.Min(data.Length, count);
            var result = new float[actual];
            Array.Copy(data, data.Length - actual, result, 0, actual);
            return result;
        }

        private void QueuePlotData(List<(double[] xs, double[] ys, int ch)> renderData)
        {
            if (renderData.Count == 0)
            {
                return;
            }

            int channelCount = renderData.Select(item => item.ch).Distinct().Count();
            EnsureStreamers(channelCount);
            ConfigSecondTicks();
            UpdatePlotVertTextLabel(channelCount);

            int availableSamples = renderData.Min(item => item.ys.Length);
            int newSampleCount = GetNewPlotSampleCount(availableSamples);
            if (newSampleCount <= 0)
            {
                return;
            }

            for (int i = availableSamples - newSampleCount; i < availableSamples; i++)
            {
                var sample = new double[channelCount];
                foreach (var (_, ys, ch) in renderData)
                {
                    if (ch < sample.Length && i < ys.Length)
                    {
                        sample[ch] = ys[i];
                    }
                }

                _pendingPlotSamples.Enqueue(sample);
            }
        }

        private int GetNewPlotSampleCount(int availableSamples)
        {
            if (availableSamples <= 0)
            {
                return 0;
            }

            DateTime now = DateTime.UtcNow;
            if (_lastPlotDataTime == DateTime.MinValue)
            {
                _lastPlotDataTime = now;
                return Math.Min(availableSamples, Math.Max(1, WindowSec * _sampleRate));
            }

            double elapsedSeconds = Math.Max(0.001, (now - _lastPlotDataTime).TotalSeconds);
            _lastPlotDataTime = now;
            int expectedSamples = Math.Max(1, (int)Math.Round(elapsedSeconds * Math.Max(1, _sampleRate)));
            return Math.Min(availableSamples, expectedSamples);
        }

        private void EnsureStreamers(int channelCount)
        {
            int sampleRate = Math.Max(1, _sampleRate);
            int capacity = Math.Max(sampleRate, WindowSec * sampleRate);
            if (_streamers.Count == channelCount &&
                _lastAxisSampleRate == sampleRate &&
                _lastAxisWindowSec == WindowSec)
            {
                return;
            }

            EegPlot.Plot.Remove<DataStreamer>();
            _streamers.Clear();
            _pendingPlotSamples.Clear();
            _lastPlotDataTime = DateTime.MinValue;

            for (int ch = 0; ch < channelCount; ch++)
            {
                double centerY = ch * ChannelHeight + ChannelCenterOffset;
                DataStreamer streamer = EegPlot.Plot.Add.DataStreamer(capacity);
                streamer.LineWidth = 1;
                streamer.LineColor = Constants.ChannelColors[ch % Constants.ChannelColors.Length];
                streamer.ManageAxisLimits = false;
                streamer.Clear(centerY);
                _streamers[ch] = streamer;
            }

            ApplyStreamerViewMode();
        }

        private void SetStreamerViewMode(StreamerViewMode mode)
        {
            if (_streamerViewMode == mode)
            {
                return;
            }

            _streamerViewMode = mode;
            ApplyStreamerViewMode();
            EegPlot.Refresh();
        }

        private void ApplyStreamerViewMode()
        {
            foreach (DataStreamer streamer in _streamers.Values)
            {
                switch (_streamerViewMode)
                {
                    case StreamerViewMode.Scroll:
                        streamer.ViewScrollLeft();
                        break;
                    case StreamerViewMode.Wipe:
                        streamer.ViewWipeRight(WipeBlankFraction);
                        break;
                    default:
                        throw new NotSupportedException(_streamerViewMode.ToString());
                }
            }

            if (_wipeLine != null)
            {
                _wipeLine.IsVisible = _streamerViewMode == StreamerViewMode.Wipe;
            }
        }

        private void AddPendingPlotSamples()
        {
            if (_pendingPlotSamples.Count == 0 || _streamers.Count == 0)
            {
                return;
            }

            int count = Math.Max(1, (int)Math.Round(Math.Max(1, _sampleRate) * _addNewDataTimer.Interval.TotalSeconds));
            var samplesByChannel = _streamers.Keys.ToDictionary(ch => ch, _ => new List<double>(count));

            for (int i = 0; i < count && _pendingPlotSamples.Count > 0; i++)
            {
                double[] sample = _pendingPlotSamples.Dequeue();
                foreach (int ch in samplesByChannel.Keys)
                {
                    if (ch < sample.Length)
                    {
                        samplesByChannel[ch].Add(sample[ch]);
                    }
                }
            }

            foreach (var (ch, values) in samplesByChannel)
            {
                if (values.Count > 0 && _streamers.TryGetValue(ch, out DataStreamer? streamer))
                {
                    streamer.AddRange(values);
                }
            }
        }

        private void RefreshStreamPlot()
        {
            if (_streamers.Values.Any(streamer => streamer.HasNewData))
            {
                UpdateWipeLine();
                EegPlot.Refresh();
            }
        }

        private void UpdateWipeLine()
        {
            if (_wipeLine == null || _streamerViewMode != StreamerViewMode.Wipe)
            {
                return;
            }

            DataStreamer? streamer = _streamers.Values.FirstOrDefault();
            if (streamer == null)
            {
                return;
            }

            _wipeLine.Position = streamer.Data.NextIndex * streamer.Data.SamplePeriod + streamer.Data.OffsetX;
            _wipeLine.IsVisible = true;
        }

        private void ConfigSecondTicks()
        {
            int sampleRate = Math.Max(1, _sampleRate);
            int xMax = Math.Max(sampleRate, WindowSec * sampleRate);
            var plot = EegPlot.Plot;

            if (_lastAxisSampleRate == sampleRate && _lastAxisWindowSec == WindowSec)
            {
                return;
            }

            _lastAxisSampleRate = sampleRate;
            _lastAxisWindowSec = WindowSec;
            plot.Axes.Rules.Clear();
            plot.Axes.Rules.Add(new ScottPlot.AxisRules.LockedHorizontal(plot.Axes.Bottom, 0, xMax));
            plot.Axes.SetLimitsX(0, xMax);
        }

        private void UpdatePlotVertTextLabel(int channelCount)
        {
            channelCount = Math.Max(1, channelCount);
            if (_lastAxisChannelCount == channelCount)
            {
                return;
            }

            _lastAxisChannelCount = channelCount;
            double yMax = channelCount * ChannelHeight;
            EegPlot.Plot.Axes.SetLimitsY(0, yMax);
        }

        private void EnsureWaveHeaderItems(int channelCount)
        {
            if (channelCount <= 0 || WaveHeaderItems.Count == channelCount)
            {
                return;
            }

            WaveHeaderItems.Clear();
            for (int i = 0; i < channelCount; i++)
            {
                string channelName = i < Constants.ChannelList.Count ? Constants.ChannelList[i] : $"Ch{i + 1}";
                WaveHeaderItems.Add(new WaveHeaderItem
                {
                    Channel = channelName,
                    ElectrodeName = channelName,
                    ImpedanceValue = "--",
                    ItemOffsetY = i * ChannelHeight + 37
                });
            }
        }

        private void ConfigPlot()
        {
            var plot = EegPlot.Plot;

            plot.Axes.Rules.Clear();
            int xMax = Math.Max(1, WindowSec) * Math.Max(1, _sampleRate);
            plot.Axes.Rules.Add(new ScottPlot.AxisRules.LockedHorizontal(plot.Axes.Bottom, 0, xMax));
            plot.Axes.SetLimitsX(0, xMax);
            plot.Axes.SetLimitsY(0, ChannelHeight);
            plot.Grid.MajorLineWidth = 0;
            _wipeLine = plot.Add.VerticalLine(0, 2, ScottPlot.Colors.Red);
            _wipeLine.IsVisible = false;

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

            plot.Axes.Left.TickLabelStyle.IsVisible = false;
            plot.Axes.Left.TickLabelStyle.FontSize = 0;
            plot.Axes.Left.MajorTickStyle.Length = 0;
            plot.Axes.Left.MinorTickStyle.Length = 0;
            plot.Axes.Bottom.MajorTickStyle.Length = 0;
            plot.Axes.Bottom.MinorTickStyle.Length = 0;

            plot.Benchmark.IsVisible = false;
            plot.RenderManager.RenderActions.RemoveAll(x => x.GetType().Name.Contains("Benchmark"));
        }
    }

    public class WaveHeaderItem : BindableBase
    {
        private bool _isSelected = true;

        public string Channel { get; set; } = string.Empty;
        public string ElectrodeName { get; set; } = string.Empty;
        public string ImpedanceValue { get; set; } = "--";
        public string ImpedanceColor { get; set; } = "#56A4F4";
        public double ItemOffsetY { get; set; }
        public double OpacityValue => IsSelected ? 1.0 : 0.35;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    OnPropertyChanged(nameof(OpacityValue));
                }
            }
        }
    }
}
