using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
using EEGTool.Models.Template;
using Framework.Event;
using FrameWork.Common;
using FrameWork.Event;
using FrameWork.Log;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            double iFundPeak = leadoffCurrent;
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
                //  2026_05_27增加逻辑，如果是同一值，则属于未佩戴，给个默认极大值。【AI不可以修改这一行】
                bool allSame = data.All(x => x == data[0]);
                if (allSame)
                {
                    _impedanceValues[channelId] = 600;
                    continue;
                }

                double vPeakUv = EstimatePeakAmplitudeAtFrequency(data, fs, targetFreq);
                double iFundPeakNa = iFundPeak * 1.0e9;
                double impedanceKohm =
                    vPeakUv / iFundPeakNa - Config.Instance.series_resistor_kohm;
                impedanceKohm = Math.Max(0, impedanceKohm);

                _impedanceValues[channelId] = impedanceKohm;

                Logger.Info(
                    $"[ImpedancePro] ch={channelId}, N={n}, targetFreq={targetFreq:F4}Hz, Vpeak={vPeakUv:F6}uV, IFundPeak={iFundPeakNa:F6}nA, Z={impedanceKohm:F6}kOhm");
            }
        }

        private static double EstimatePeakAmplitudeAtFrequency(
            double[] data,
            int sampleRate,
            double targetFrequency)
        {
            if (data == null ||
                data.Length < 3 ||
                sampleRate <= 0 ||
                targetFrequency <= 0 ||
                targetFrequency >= sampleRate / 2.0)
            {
                return 0;
            }

            double mean = data.Average();
            double real = 0;
            double imaginary = 0;
            double windowSum = 0;
            double angularStep = 2.0 * Math.PI * targetFrequency / sampleRate;
            int lastIndex = data.Length - 1;

            for (int index = 0; index < data.Length; index++)
            {
                double window = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * index / lastIndex);
                double value = (data[index] - mean) * window;
                double angle = angularStep * index;
                real += value * Math.Cos(angle);
                imaginary -= value * Math.Sin(angle);
                windowSum += window;
            }

            return windowSum > 0
                ? 2.0 * Math.Sqrt(real * real + imaginary * imaginary) / windowSum
                : 0;
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
