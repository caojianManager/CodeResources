using EEGTool.Models.Collection;
using EEGTool.Models.Template;
using Framework.Event;
using Framework.MVVM.Commands;
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
using System.Windows.Input;
using System.Windows.Threading;
using Color = System.Drawing.Color;

namespace EEGTool.ViewModels.Impedance
{
    public class ImpedanceEEGViewModel : BindableBase
    {
        private const double ChannelHeight = 1.0;
        private const double WaveAmplitude = 0.38;
        private const float ChannelDividerLineWidth = 0.5f;
        private const int WindowSeconds = 5;
        private const int MaxQueueLatencyMilliseconds = 150;

        private readonly object _dataLock = new();
        private readonly Dictionary<int, DataStreamer> _streamers = new();
        private readonly List<HorizontalLine> _channelDividerLines = new();
        private readonly Queue<double[]> _pendingSamples = new();
        private readonly DispatcherTimer _sampleTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(10)
        };
        private readonly DispatcherTimer _renderTimer = new(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };

        private VerticalLine? _wipeLine;
        private int _sampleRate = 250;
        private int _channelCount;
        private int _lastWindowSampleCount;
        private int _streamerSampleRate;
        private long _receivedVersion;

        public WpfPlot ScottPlotEEG { get; } = new();
        public ObservableCollection<ImpedanceChannelHeader> ChannelHeaders { get; } = new();
        public ObservableCollection<ImpedanceChannelDivider> ChannelDividerItems { get; } = new();
        public ICommand ClickChannelHeaderCommand { get; }

        private double _verticalScaleMicrovolts = 200;
        public double VerticalScaleMicrovolts
        {
            get => _verticalScaleMicrovolts;
            set => SetProperty(ref _verticalScaleMicrovolts, Math.Max(1, value));
        }

        public ImpedanceEEGViewModel()
        {
            ConfigurePlot();

            ClickChannelHeaderCommand = new RelayCommand(parameter =>
            {
                if (parameter is not ImpedanceChannelHeader header)
                {
                    return;
                }

                header.IsSelected = !header.IsSelected;
                if (_streamers.TryGetValue(header.ChannelIndex, out DataStreamer? streamer))
                {
                    streamer.IsVisible = header.IsSelected;
                    ScottPlotEEG.Refresh();
                }
            });

            _sampleTimer.Tick += (_, _) => AddPendingSamples();
            _renderTimer.Tick += (_, _) => RenderPlot();
            _sampleTimer.Start();
            _renderTimer.Start();

            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(
                EventName.RECEVIED_IMPEDANCE_DATA,
                ReceivedData);
        }

        private void ReceivedData(DataProcessingResult result)
        {
            if (result == null)
            {
                return;
            }

            float[][] source = SelectSource(result);
            if (source.Length == 0)
            {
                return;
            }

            int sampleRate = Math.Max(1, CollectionInfoManager.GetInstance().Info.SampleRate);
            int windowSampleCount = source.Where(channel => channel != null)
                .Select(channel => channel.Length)
                .DefaultIfEmpty(0)
                .Min();
            if (windowSampleCount <= 0)
            {
                return;
            }

            int previousWindowCount;
            long version;
            lock (_dataLock)
            {
                previousWindowCount = _lastWindowSampleCount;
                _lastWindowSampleCount = windowSampleCount;
                _sampleRate = sampleRate;
                version = ++_receivedVersion;
            }

            int newSampleCount = previousWindowCount <= 0
                ? Math.Min(windowSampleCount, Math.Max(1, sampleRate / 25))
                : Math.Clamp(windowSampleCount - previousWindowCount, 0, windowSampleCount);
            if (newSampleCount == 0 && windowSampleCount >= WindowSeconds * sampleRate)
            {
                newSampleCount = Math.Min(windowSampleCount, Math.Max(1, sampleRate / 25));
            }

            double[][] scaledChannels = BuildScaledChannels(source, windowSampleCount);
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (version < _receivedVersion)
                {
                    return;
                }

                EnsurePlotChannels(scaledChannels.Length, sampleRate);
                QueueNewestSamples(scaledChannels, newSampleCount);
            });
        }

        private static float[][] SelectSource(DataProcessingResult result)
        {
            float[][] source = result.DisplayWindowByChannel;
            if (source.Any(channel => channel != null && channel.Length > 0))
            {
                return source;
            }

            return result.FilteredByChannel;
        }

        private double[][] BuildScaledChannels(float[][] source, int sampleCount)
        {
            var result = new double[source.Length][];
            for (int channel = 0; channel < source.Length; channel++)
            {
                float[] channelData = source[channel] ?? Array.Empty<float>();
                int count = Math.Min(sampleCount, channelData.Length);
                int start = channelData.Length - count;
                double mean = count == 0
                    ? 0
                    : channelData.Skip(start).Take(count).Average(value => (double)value);
                double center = GetChannelCenter(channel, source.Length);
                double scale = WaveAmplitude / Math.Max(1, VerticalScaleMicrovolts);

                result[channel] = new double[count];
                for (int sample = 0; sample < count; sample++)
                {
                    double centeredMicrovolts = channelData[start + sample] - mean;
                    result[channel][sample] = center + Math.Clamp(
                        centeredMicrovolts * scale,
                        -WaveAmplitude,
                        WaveAmplitude);
                }
            }

            return result;
        }

        private void EnsurePlotChannels(int channelCount, int sampleRate)
        {
            if (_channelCount == channelCount &&
                _streamerSampleRate == sampleRate &&
                _streamers.Count == channelCount)
            {
                return;
            }

            ScottPlotEEG.Plot.Remove<DataStreamer>();
            ClearChannelDividerLines();
            _streamers.Clear();
            _pendingSamples.Clear();
            _channelCount = channelCount;
            _streamerSampleRate = sampleRate;

            int capacity = Math.Max(1, WindowSeconds * sampleRate);
            for (int channel = 0; channel < channelCount; channel++)
            {
                DataStreamer streamer = ScottPlotEEG.Plot.Add.DataStreamer(capacity);
                streamer.LineWidth = 1.3f;
                streamer.LineColor = Constants.ChannelColors[channel % Constants.ChannelColors.Length];
                streamer.ManageAxisLimits = false;
                streamer.ViewScrollLeft();
                _streamers[channel] = streamer;
            }

            BuildChannelHeaders(channelCount);
            BuildChannelDividerItems(channelCount);
            BuildChannelDividerLines(channelCount);
            ScottPlotEEG.Plot.Axes.SetLimitsX(0, capacity);
            ScottPlotEEG.Plot.Axes.SetLimitsY(0, Math.Max(1, channelCount));
            ScottPlotEEG.Refresh();
            UpdateChannelHeaderPositions();
        }

        private void ClearChannelDividerLines()
        {
            foreach (HorizontalLine line in _channelDividerLines)
            {
                ScottPlotEEG.Plot.Remove(line);
            }

            _channelDividerLines.Clear();
        }

        private void BuildChannelDividerLines(int channelCount)
        {
            for (int channelBoundary = 1; channelBoundary < channelCount; channelBoundary++)
            {
                HorizontalLine dividerLine = ScottPlotEEG.Plot.Add.HorizontalLine(
                    channelBoundary,
                    ChannelDividerLineWidth,
                    ScottPlot.Color.FromColor(System.Drawing.Color.Gray));
                _channelDividerLines.Add(dividerLine);
            }
        }

        private void QueueNewestSamples(double[][] channels, int newSampleCount)
        {
            if (channels.Length == 0 || newSampleCount <= 0)
            {
                return;
            }

            int available = channels.Min(channel => channel.Length);
            int count = Math.Min(newSampleCount, available);
            int maxQueued = Math.Max(1, _sampleRate * MaxQueueLatencyMilliseconds / 1000);
            while (_pendingSamples.Count + count > maxQueued && _pendingSamples.Count > 0)
            {
                _pendingSamples.Dequeue();
            }

            count = Math.Min(count, maxQueued);
            for (int sample = available - count; sample < available; sample++)
            {
                var values = new double[channels.Length];
                for (int channel = 0; channel < channels.Length; channel++)
                {
                    values[channel] = channels[channel][sample];
                }

                _pendingSamples.Enqueue(values);
            }
        }

        private void AddPendingSamples()
        {
            if (_pendingSamples.Count == 0 || _streamers.Count == 0)
            {
                return;
            }

            int count = Math.Max(
                1,
                (int)Math.Round(_sampleRate * _sampleTimer.Interval.TotalSeconds));
            var channelValues = _streamers.Keys.ToDictionary(
                channel => channel,
                _ => new List<double>(count));

            for (int sample = 0; sample < count && _pendingSamples.Count > 0; sample++)
            {
                double[] values = _pendingSamples.Dequeue();
                foreach (int channel in channelValues.Keys)
                {
                    if (channel < values.Length)
                    {
                        channelValues[channel].Add(values[channel]);
                    }
                }
            }

            foreach ((int channel, List<double> values) in channelValues)
            {
                if (values.Count > 0)
                {
                    _streamers[channel].AddRange(values);
                }
            }
        }

        private void RenderPlot()
        {
            if (!_streamers.Values.Any(streamer => streamer.HasNewData))
            {
                return;
            }

            ScottPlotEEG.Refresh();
            UpdateChannelHeaderPositions();
        }

        private void BuildChannelHeaders(int channelCount)
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

            ChannelHeaders.Clear();
            for (int index = 0; index < channelCount; index++)
            {
                string channelId = $"Ch{enabledChannels[index]}";
                string electrode = TemplateFileManager.GetInstance().GetChannelName(channelId, template);
                ChannelHeaders.Add(new ImpedanceChannelHeader
                {
                    ChannelIndex = index,
                    ChannelId = channelId,
                    EleTag = string.IsNullOrWhiteSpace(electrode) ? "--" : electrode,
                    ImpedanceValue = "--",
                    ImpedanceColor = Constants.ChannelColors[index % Constants.ChannelColors.Length].ToHex()
                });
            }
        }

        private void BuildChannelDividerItems(int channelCount)
        {
            ChannelDividerItems.Clear();
            for (int channelBoundary = 1; channelBoundary < channelCount; channelBoundary++)
            {
                ChannelDividerItems.Add(new ImpedanceChannelDivider
                {
                    BoundaryY = channelBoundary
                });
            }
        }

        public void UpdateChannelHeaderPositions()
        {
            if (ChannelHeaders.Count == 0)
            {
                return;
            }

            PixelRect dataRect = ScottPlotEEG.Plot.RenderManager.LastRender.DataRect;
            if (dataRect.Height <= 0)
            {
                return;
            }

            for (int i = 0; i < ChannelHeaders.Count; i++)
            {
                double topY = ChannelHeaders.Count - i;
                double bottomY = ChannelHeaders.Count - i - 1;
                float topPixelY = ScottPlotEEG.Plot.Axes.Left.GetPixel(topY, dataRect);
                float bottomPixelY = ScottPlotEEG.Plot.Axes.Left.GetPixel(bottomY, dataRect);

                ChannelHeaders[i].ItemOffsetY = topPixelY;
                ChannelHeaders[i].ItemHeight = Math.Max(1, bottomPixelY - topPixelY);
            }

            foreach (ImpedanceChannelDivider divider in ChannelDividerItems)
            {
                divider.ItemOffsetY = ScottPlotEEG.Plot.Axes.Left.GetPixel(divider.BoundaryY, dataRect);
            }
        }

        private void ConfigurePlot()
        {
            Plot plot = ScottPlotEEG.Plot;
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#263441");
            plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#1B2730");
     

            plot.Axes.Right.FrameLineStyle.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
            plot.Axes.Right.FrameLineStyle.Width = 1;
            plot.Axes.Right.FrameLineStyle.Pattern = LinePattern.Solid;

            plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
            plot.Axes.Bottom.FrameLineStyle.Width = 1;
            plot.Axes.Bottom.FrameLineStyle.Pattern = LinePattern.Solid;

            plot.Axes.Left.FrameLineStyle.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
            plot.Axes.Left.FrameLineStyle.Width = 1;
            plot.Axes.Left.FrameLineStyle.Pattern = LinePattern.Solid;

            plot.Axes.Top.FrameLineStyle.Color = ScottPlot.Color.FromColor(System.Drawing.Color.Gray);
            plot.Axes.Top.FrameLineStyle.Width = 1;
            plot.Axes.Top.FrameLineStyle.Pattern = LinePattern.Solid;

            plot.Axes.Left.TickLabelStyle.IsVisible = false;
            plot.Axes.Left.TickLabelStyle.FontSize = 0;
            plot.Axes.Left.MajorTickStyle.Length = 0;
            plot.Axes.Left.MinorTickStyle.Length = 0;
            plot.Axes.Bottom.MajorTickStyle.Length = 0;
            plot.Axes.Bottom.MinorTickStyle.Length = 0;
            plot.Axes.Bottom.TickLabelStyle.IsVisible = false;

            plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Color.FromHex("#455A64");
            plot.Benchmark.IsVisible = false;
            plot.Grid.MajorLineWidth = 0;

            _wipeLine = plot.Add.VerticalLine(0, 1, ScottPlot.Color.FromHex("#90A4AE"));
            _wipeLine.LinePattern = LinePattern.Dotted;
            _wipeLine.IsVisible = false;
            ScottPlotEEG.UserInputProcessor.Disable();
        }

        private static double GetChannelCenter(int channelIndex, int channelCount)
        {
            return (channelCount - channelIndex - 1) * ChannelHeight + ChannelHeight / 2.0;
        }
    }

    public class ImpedanceChannelHeader : BindableBase
    {
        private bool _isSelected = true;
        private double _itemOffsetY;
        private double _itemHeight = 50.0;

        public int ChannelIndex { get; set; }
        public string ChannelId { get; set; } = string.Empty;
        public string EleTag { get; set; } = string.Empty;
        public string ImpedanceValue { get; set; } = "--";
        public string ImpedanceColor { get; set; } = "#56A4F4";
        public double ItemOffsetY
        {
            get => _itemOffsetY;
            set => SetProperty(ref _itemOffsetY, value);
        }
        public double ItemHeight
        {
            get => _itemHeight;
            set => SetProperty(ref _itemHeight, value);
        }
        public double OpacityValue => IsSelected ? 1 : 0.35;

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

    public class ImpedanceChannelDivider : BindableBase
    {
        private double _itemOffsetY;

        public double BoundaryY { get; set; }
        public double ItemOffsetY
        {
            get => _itemOffsetY;
            set => SetProperty(ref _itemOffsetY, value);
        }
    }
}
