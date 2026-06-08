using EEGTool.Models;
using FrameWork.Common;
using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

public sealed class DataProcessor
{
    private readonly MultiChannelRingBuffer _buffer;
    private readonly ISignalFilter _filter;
    private readonly IFftProcessor _fft;

    public DataProcessorSettings Settings { get; }

    public DataProcessor(
        MultiChannelRingBuffer buffer,
        ISignalFilter filter,
        IFftProcessor fft,
        DataProcessorSettings? settings = null)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _fft = fft ?? throw new ArgumentNullException(nameof(fft));
        Settings = settings ?? new DataProcessorSettings(_buffer.ChannelCount, _buffer.SampleRate);
    }

    public DataProcessingResult Process()
    {
        int channels = _buffer.ChannelCount;
        int sampleRate = _buffer.SampleRate;

        var result = new DataProcessingResult(channels, Settings.Bands.Length);

        for (int ch = 0; ch < channels; ch++)
        {
            float[] raw = _buffer.GetRawChannelSnapshot(ch);
            if (raw.Length == 0)
                continue;

            double[] filtered = Array.ConvertAll(raw, x => (double)x);

            if (Settings.BandStop[ch].Enabled)
            {
                var s = Settings.BandStop[ch];
                _filter.BandStop(filtered, sampleRate, s.StartHz, s.StopHz, s.Order, s.Type);
            }

            if (Settings.BandPass[ch].Enabled)
            {
                var s = Settings.BandPass[ch];
                _filter.BandPass(filtered, sampleRate, s.StartHz, s.StopHz, s.Order, s.Type);
            }

            switch (Settings.EnvironmentalNoise)
            {
                case EnvironmentalNoiseMode.Hz50:
                    _filter.RemoveEnvironmentalNoise(filtered, sampleRate, 50);
                    break;
                case EnvironmentalNoiseMode.Hz60:
                    _filter.RemoveEnvironmentalNoise(filtered, sampleRate, 60);
                    break;
                case EnvironmentalNoiseMode.Hz50And60:
                    _filter.RemoveEnvironmentalNoise(filtered, sampleRate, 50);
                    _filter.RemoveEnvironmentalNoise(filtered, sampleRate, 60);
                    break;
            }

            result.RawByChannel[ch] = raw;
            result.FilteredByChannel[ch] = Array.ConvertAll(filtered, x => (float)x);

            int oneSecondCount = Math.Min(sampleRate, result.FilteredByChannel[ch].Length);
            result.StdByChannel[ch] = StdOfLast(result.FilteredByChannel[ch], oneSecondCount);

            float[] fftSource = Settings.UseFilteredDataForFft
                ? result.FilteredByChannel[ch]
                : raw;

            int nfft = GetSafeNfft(sampleRate);
            float[] fftWindow = TakeLast(fftSource, nfft);
            RemoveMeanInPlace(fftWindow);

            float[] amplitudeSpectrum = _fft.ComputeAmplitudeSpectrum(fftWindow, sampleRate);
            result.FftAmplitudeByChannel[ch] = amplitudeSpectrum;

            for (int b = 0; b < Settings.Bands.Length; b++)
            {
                var band = Settings.Bands[b];
                result.BandPowerByChannel[ch, b] =
                    ComputeBandPower(amplitudeSpectrum, sampleRate, fftWindow.Length, band.LowHz, band.HighHz);
            }

            result.DisplayWindowByChannel[ch] =
                TakeLast(result.FilteredByChannel[ch], Settings.DisplaySeconds * sampleRate);
        }

        int refChannel = IndexOfMax(result.StdByChannel);
        result.ReferenceChannel = refChannel;

        if (refChannel >= 0 && result.FilteredByChannel[refChannel] != null)
        {
            float[] ref1s = TakeLast(result.FilteredByChannel[refChannel], Math.Min(sampleRate, result.FilteredByChannel[refChannel].Length));

            for (int ch = 0; ch < channels; ch++)
            {
                float[] cur1s = TakeLast(result.FilteredByChannel[ch], Math.Min(sampleRate, result.FilteredByChannel[ch].Length));
                result.PolarityByChannel[ch] = Dot(cur1s, ref1s) >= 0f ? 1f : -1f;
            }
        }

        for (int b = 0; b < Settings.Bands.Length; b++)
        {
            float sum = 0f;
            for (int ch = 0; ch < channels; ch++)
                sum += result.BandPowerByChannel[ch, b];

            result.HeadWideBandPower[b] = channels > 0 ? sum / channels : 0f;
        }

        return result;
    }

    private static int GetSafeNfft(int sampleRate)
    {
        return sampleRate switch
        {
            500 => 512,
            1000 => 1024,
            1600 => 2048,
            _ => 256
        };
    }

    private static float[] TakeLast(float[] data, int count)
    {
        if (data == null || data.Length == 0 || count <= 0)
            return Array.Empty<float>();

        int actual = Math.Min(count, data.Length);
        var result = new float[actual];
        Array.Copy(data, data.Length - actual, result, 0, actual);
        return result;
    }

    private static void RemoveMeanInPlace(float[] data)
    {
        if (data.Length == 0) return;
        float mean = data.Average();
        for (int i = 0; i < data.Length; i++)
            data[i] -= mean;
    }

    private static float StdOfLast(float[] data, int count)
    {
        if (data.Length == 0 || count <= 0) return 0f;

        int start = data.Length - count;
        float mean = 0f;
        for (int i = start; i < data.Length; i++) mean += data[i];
        mean /= count;

        float sumSq = 0f;
        for (int i = start; i < data.Length; i++)
        {
            float d = data[i] - mean;
            sumSq += d * d;
        }

        return MathF.Sqrt(sumSq / count);
    }

    private static float Dot(float[] a, float[] b)
    {
        int n = Math.Min(a.Length, b.Length);
        float sum = 0f;
        for (int i = 0; i < n; i++) sum += a[i] * b[i];
        return sum;
    }

    private static int IndexOfMax(float[] values)
    {
        if (values == null || values.Length == 0) return -1;
        int idx = 0;
        float max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
                idx = i;
            }
        }
        return idx;
    }

    private static float ComputeBandPower(float[] amplitudeSpectrum, int sampleRate, int nfft, float lowHz, float highHz)
    {
        if (amplitudeSpectrum == null || amplitudeSpectrum.Length == 0 || nfft <= 0)
            return 0f;

        float sum = 0f;
        float binWidth = (float)sampleRate / nfft;

        for (int i = 0; i < amplitudeSpectrum.Length; i++)
        {
            float f = i * binWidth;
            if (f < lowHz || f >= highHz) continue;

            float mag = amplitudeSpectrum[i];
            float psd = (i != 0 && i != nfft / 2)
                ? mag * mag * nfft / sampleRate / 4f
                : mag * mag * nfft / sampleRate;

            sum += psd;
        }

        return sum;
    }
}

public sealed class DataProcessorSettings
{
    public int ChannelCount { get; }
    public int SampleRate { get; }
    public int DisplaySeconds { get; set; } = 20;
    public bool UseFilteredDataForFft { get; set; } = true;
    public EnvironmentalNoiseMode EnvironmentalNoise { get; set; } = EnvironmentalNoiseMode.Hz50And60;

    public FilterSpec[] BandPass { get; }
    public FilterSpec[] BandStop { get; }

    public FrequencyBand[] Bands { get; } =
    {
        new FrequencyBand(1, 4),
        new FrequencyBand(4, 8),
        new FrequencyBand(8, 13),
        new FrequencyBand(13, 30),
        new FrequencyBand(30, 55)
    };

    public DataProcessorSettings(int channelCount, int sampleRate)
    {
        ChannelCount = channelCount;
        SampleRate = sampleRate;

        BandPass = Enumerable.Range(0, channelCount)
            .Select(_ => new FilterSpec(true, 5, 50, 4, FilterKind.Butterworth))
            .ToArray();

        BandStop = Enumerable.Range(0, channelCount)
            .Select(_ => new FilterSpec(false, 58, 62, 4, FilterKind.Butterworth))
            .ToArray();
    }
}

public sealed class DataProcessingResult
{
    public float[][] RawByChannel { get; }
    public float[][] FilteredByChannel { get; }
    public float[][] DisplayWindowByChannel { get; }
    public float[][] FftAmplitudeByChannel { get; }
    public float[] StdByChannel { get; }
    public float[] PolarityByChannel { get; }
    public float[,] BandPowerByChannel { get; }
    public float[] HeadWideBandPower { get; }
    public int ReferenceChannel { get; set; } = -1;

    public DataProcessingResult(int channels, int bands)
    {
        RawByChannel = new float[channels][];
        FilteredByChannel = new float[channels][];
        DisplayWindowByChannel = new float[channels][];
        FftAmplitudeByChannel = new float[channels][];
        StdByChannel = new float[channels];
        PolarityByChannel = new float[channels];
        BandPowerByChannel = new float[channels, bands];
        HeadWideBandPower = new float[bands];
    }
}

public readonly record struct FrequencyBand(float LowHz, float HighHz);

public sealed class FilterSpec
{
    public bool Enabled { get; set; }
    public double StartHz { get; set; }
    public double StopHz { get; set; }
    public int Order { get; set; }
    public FilterKind Type { get; set; }

    public FilterSpec(bool enabled, double startHz, double stopHz, int order, FilterKind type)
    {
        Enabled = enabled;
        StartHz = startHz;
        StopHz = stopHz;
        Order = order;
        Type = type;
    }
}

public enum FilterKind
{
    Butterworth,
    Chebyshev,
    Bessel
}

public enum EnvironmentalNoiseMode
{
    None,
    Hz50,
    Hz60,
    Hz50And60
}

public interface ISignalFilter
{
    void BandPass(double[] data, int sampleRate, double startHz, double stopHz, int order, FilterKind type);
    void BandStop(double[] data, int sampleRate, double startHz, double stopHz, int order, FilterKind type);
    void RemoveEnvironmentalNoise(double[] data, int sampleRate, int noiseHz);
}

public interface IFftProcessor
{
    float[] ComputeAmplitudeSpectrum(float[] timeData, int sampleRate);
}
