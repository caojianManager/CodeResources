using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
using EEGTool.Models.Template;
using Framework.Event;
using FrameWork.Common;
using FrameWork.Event;
using FrameWork.Log;
using FrameWork.MVVM;
using MathNet.Numerics.IntegralTransforms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Windows;

namespace EEGTool.ViewModels.Impedance
{
    public class ImpedanceListViewModel : BindableBase
    {
        private readonly Dictionary<string, double> _impedanceValues = new();
        private readonly object _calculationLock = new();
        private int _channelCount;

        public ImpedanceListViewModel()
        {
            ThresholdGoodKohm = 10;
            ThresholdWarnKohm = 50;
            RefreshItems(0);

            EventUtilManager.EventUitl.AddEvent<DataProcessingResult>(
                EventName.RECEVIED_IMPEDANCE_DATA,
                ReceivedData);
        }

        public ObservableCollection<ImpedanceListItem> Items { get; } = new();

        private double _thresholdGoodKohm;
        public double ThresholdGoodKohm
        {
            get => _thresholdGoodKohm;
            set
            {
                if (SetProperty(ref _thresholdGoodKohm, Math.Max(0, value)))
                {
                    ApplyThresholds();
                }
            }
        }

        private double _thresholdWarnKohm;
        public double ThresholdWarnKohm
        {
            get => _thresholdWarnKohm;
            set
            {
                if (SetProperty(ref _thresholdWarnKohm, Math.Max(0, value)))
                {
                    ApplyThresholds();
                }
            }
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

            lock (_calculationLock)
            {
                CalculateImpedanceValuesPro(source);
            }

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                RefreshItems(source.Length);
                UpdateItemValues();
            });
        }

        public void CalculateImpedanceValuesPro(float[][] source)
        {
            if (source == null || source.Length == 0)
            {
                return;
            }

            _impedanceValues.Clear();

            List<int> channelList = GetEnabledChannels(source.Length);
            int fs = GetSampleRate();
            double targetFreq = Config.Instance.Impedance_TargetFreq;
            double leadoffCurrent = Config.Instance.Lead_Of;
            double iFundPeak = leadoffCurrent * (4.0 / Math.PI);
            if (iFundPeak <= 0)
            {
                return;
            }

            Logger.Info($"[ImpedancePro] Start fs={fs}, targetFreq={targetFreq:F4}Hz, LeadOffPeak={leadoffCurrent:E6}A, IFundPeak={iFundPeak:E6}A, seriesR={Config.Instance.series_resistor_kohm}kOhm, channels={source.Length}");

            for (int channelIndex = 0; channelIndex < source.Length; channelIndex++)
            {
                string channelId = channelIndex < channelList.Count
                    ? channelList[channelIndex].ToString()
                    : (channelIndex + 1).ToString();
                float[] channelData = source[channelIndex] ?? Array.Empty<float>();
                double[] data = TakeLatestWindow(channelData, fs, 2);

                int n = data.Length;
                if (n <= 2)
                {
                    continue;
                }

                double mean = data.Average();
                double[] window = MathNet.Numerics.Window.Hann(n);
                var fftData = new Complex[n];
                for (int sample = 0; sample < n; sample++)
                {
                    fftData[sample] = new Complex(
                        (data[sample] - mean) * window[sample],
                        0);
                }

                Fourier.Forward(fftData, FourierOptions.Matlab);

                int rfftLength = n / 2 + 1;
                double frequencyResolution = fs / (double)n;
                int frequencyIndex = (int)Math.Round(
                    targetFreq / frequencyResolution,
                    MidpointRounding.AwayFromZero);
                frequencyIndex = Math.Clamp(frequencyIndex, 0, rfftLength - 1);

                int startIndex = Math.Max(0, frequencyIndex - 1);
                int endIndex = Math.Min(rfftLength, frequencyIndex + 2);
                double sumSquares = 0;
                for (int index = startIndex; index < endIndex; index++)
                {
                    // 与 Python 保持一致:
                    // np.abs(fft_data) / (N / 2) * 2.0
                    double magnitudeUv =
                        fftData[index].Magnitude / (n / 2.0) * 2.0;
                    sumSquares += magnitudeUv * magnitudeUv;
                }

                double vPeakUv = Math.Sqrt(sumSquares);
                double iFundPeakNa = iFundPeak * 1.0e9;
                double impedanceKohm =
                    vPeakUv / iFundPeakNa - Config.Instance.series_resistor_kohm;
                impedanceKohm = Math.Max(0, impedanceKohm);

                _impedanceValues[channelId] = impedanceKohm;

                Logger.Info(
                    $"[ImpedancePro] ch={channelId}, N={n}, targetFreq={targetFreq:F4}Hz, freqResolution={frequencyResolution:F6}Hz, freqIdx={frequencyIndex}, binRange=[{startIndex},{endIndex}), Vpeak={vPeakUv:F6}uV, IFundPeak={iFundPeakNa:F6}nA, seriesR={Config.Instance.series_resistor_kohm:F6}kOhm, Z={impedanceKohm:F6}kOhm");
            }
        }

        private void RefreshItems(int channelCount)
        {
            if (channelCount > 0 && _channelCount == channelCount && Items.Count == channelCount)
            {
                return;
            }

            _channelCount = channelCount;
            List<int> enabledChannels = GetEnabledChannels(channelCount);
            Items.Clear();

            var template = CollectionInfoManager.GetInstance().Info.Template;
            for (int index = 0; index < enabledChannels.Count; index++)
            {
                string channelId = $"Ch{enabledChannels[index]}";
                string electrode = TemplateFileManager.GetInstance().GetChannelName(channelId, template);
                Items.Add(new ImpedanceListItem
                {
                    ChannelNumber = enabledChannels[index],
                    NameAndChannel = string.IsNullOrWhiteSpace(electrode)
                        ? channelId
                        : $"{electrode}/{channelId}",
                    ImpedanceValue = "--",
                    ImpedanceStatus = "--",
                    ImpedanceBrush = "#808080"
                });
            }
        }

        private void UpdateItemValues()
        {
            foreach (ImpedanceListItem item in Items)
            {                
                string key = item.ChannelNumber.ToString();
                if (!_impedanceValues.TryGetValue(key, out double value))
                {
                    item.ImpedanceValue = "--";
                    item.ImpedanceStatus = "--";
                    item.ImpedanceBrush = "#808080";
                    continue;
                }

                item.ImpedanceKohm = value;
                item.ImpedanceValue = $"{value:F1} kΩ";
                ApplyThreshold(item);
            }

        }

        private void ApplyThresholds()
        {
            foreach (ImpedanceListItem item in Items)
            {
                ApplyThreshold(item);
            }
        }

        private void ApplyThreshold(ImpedanceListItem item)
        {
            if (item.ImpedanceKohm < 0)
            {
                return;
            }

            double good = Math.Min(ThresholdGoodKohm, ThresholdWarnKohm);
            double warn = Math.Max(ThresholdGoodKohm, ThresholdWarnKohm);
            if (item.ImpedanceKohm <= good)
            {
                item.ImpedanceStatus = "良好";
                item.ImpedanceBrush = "#2E7D32";
            }
            else if (item.ImpedanceKohm <= warn)
            {
                item.ImpedanceStatus = "偏高";
                item.ImpedanceBrush = "#F57C00";
            }
            else
            {
                item.ImpedanceStatus = "过高";
                item.ImpedanceBrush = "#D32F2F";
            }
        }

        private static float[][] SelectSource(DataProcessingResult result)
        {
            if (result.RawByChannel.Any(channel => channel != null && channel.Length > 0))
            {
                return result.RawByChannel;
            }

            if (result.DisplayWindowByChannel.Any(channel => channel != null && channel.Length > 0))
            {
                return result.DisplayWindowByChannel;
            }

            return result.FilteredByChannel;
        }

        private static double[] TakeLatestWindow(float[] data, int sampleRate, int seconds)
        {
            if (data == null || data.Length == 0)
            {
                return Array.Empty<double>();
            }

            int count = Math.Min(data.Length, Math.Max(1, sampleRate * seconds));
            int start = data.Length - count;
            var result = new double[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = data[start + i];
            }

            return result;
        }

        private static int GetSampleRate()
        {
            if (Config.Instance.Impedance_SampleRate > 0)
            {
                return Config.Instance.Impedance_SampleRate;
            }

            int collectionSampleRate = CollectionInfoManager.GetInstance().Info.SampleRate;
            return collectionSampleRate > 0 ? collectionSampleRate : 250;
        }

        private static List<int> GetEnabledChannels(int channelCount)
        {
            var template = CollectionInfoManager.GetInstance().Info.Template;
            List<int> enabledChannels = TemplateFileManager.GetInstance()
                .GetCurrentChannelList(template)
                .Where(channel => channel >= 1 && channel <= CommandManager.ChannelCount)
                .Distinct()
                .OrderBy(channel => channel)
                .ToList();

            if (channelCount > 0 && enabledChannels.Count != channelCount)
            {
                enabledChannels = Enumerable.Range(1, channelCount).ToList();
            }

            return enabledChannels;
        }
    }

    public class ImpedanceListItem : BindableBase
    {
        public int ChannelNumber { get; set; }

        private string _nameAndChannel = string.Empty;
        public string NameAndChannel
        {
            get => _nameAndChannel;
            set => SetProperty(ref _nameAndChannel, value);
        }

        private string _impedanceValue = "--";
        public string ImpedanceValue
        {
            get => _impedanceValue;
            set => SetProperty(ref _impedanceValue, value);
        }

        private string _impedanceStatus = "--";
        public string ImpedanceStatus
        {
            get => _impedanceStatus;
            set => SetProperty(ref _impedanceStatus, value);
        }

        private string _impedanceBrush = "#808080";
        public string ImpedanceBrush
        {
            get => _impedanceBrush;
            set => SetProperty(ref _impedanceBrush, value);
        }

        public double ImpedanceKohm { get; set; } = -1;
    }
}
