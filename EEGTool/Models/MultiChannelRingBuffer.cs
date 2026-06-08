using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Logging;
using Logger = FrameWork.Log.Logger;

namespace EEGTool.Models
{
    public class MultiChannelRingBuffer
    {
        private readonly int _channelCount;
        private readonly int _sampleRate;
        private readonly int _bufferSeconds;
        private readonly int _capacity;

        private readonly float[][] _rawBuffer;
        private readonly float[][] _filteredBuffer;

        private int _writeIndex = 0;
        private int _count = 0;

        public int ChannelCount => _channelCount;
        public int SampleRate => _sampleRate;
        public int BufferSeconds => _bufferSeconds;
        public int Capacity => _capacity;
        public int Count => _count;

        public MultiChannelRingBuffer(int channelCount, int sampleRate, int bufferSeconds = 22)
        {
            if (channelCount <= 0)
            {
                Logger.Error("[MultiChannelRingBuffer]:通道数量不能空!");
                return;
            }

            if (sampleRate <= 0)
            {
                Logger.Error("[MultiChannelRingBuffer]:采样率不能为0!");
                return;
            }

            if (bufferSeconds <= 0)
            {
                Logger.Error("[MultiChannelRingBuffer]:缓冲时长大于0!");
                return;
            }

            _channelCount = channelCount;
            _sampleRate = sampleRate;
            _bufferSeconds = bufferSeconds;
            _capacity = sampleRate * bufferSeconds;

            _rawBuffer = new float[channelCount][];
            _filteredBuffer = new float[channelCount][];

            for (int ch = 0; ch < channelCount; ch++)
            {
                _rawBuffer[ch] = new float[_capacity];
                _filteredBuffer[ch] = new float[_capacity];
            }
        }


        #region 写入数据

        /// <summary>
        /// 写入一帧数据 比如16通道采集，channelValues的长度就是16
        /// </summary>
        /// <param name="channelValues"></param>
        public void AddSample(float[] channelValues)
        {
            if (channelValues == null)
            {
                Logger.Error("[MultiChannelRingBuffer][AddSample]:通道值不可以为空!");
                return;
            }

            if (channelValues.Length != _channelCount)
            {
                Logger.Error("[MultiChannelRingBuffer][AddSample]:通道数值个数和通道数不一致!");
                return;
            }

            for (int ch = 0; ch < _channelCount; ch++)
            {
                _rawBuffer[ch][_writeIndex] = channelValues[ch];
                _filteredBuffer[ch][_writeIndex] = channelValues[ch]; // 先复制一份，后面滤波可覆盖
            }

            _writeIndex = (_writeIndex + 1) % _capacity;

            if (_count < _capacity)
                _count++;
        }

        /// <summary>
        /// 批量写入数据
        /// </summary>
        /// <param name="samples"></param>
        public void AddBlock(float[][] samples)
        {
            if (samples == null)
            {
                Logger.Error("[MultiChannelRingBuffer][AddBlock]:写入数据不可以为空!");
                return;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                AddSample(samples[i]);
            }
        }

        #endregion

        #region 获取数据


        /// <summary>
        /// 获取某个通道此刻数据的快照
        /// </summary>
        /// <param name="channel">第一个通道</param>
        /// <returns></returns>
        public float[] GetRawChannelSnapshot(int channel)
        {
            ValidateChannel(channel);
            return GetOrderedSnapshot(_rawBuffer[channel]);
        }

        public float[] GetFilteredChannelSnapshot(int channel)
        {
            ValidateChannel(channel);
            return GetOrderedSnapshot(_filteredBuffer[channel]);
        }

        // 取最新 N 秒窗口
        public float[] GetRawWindow(int channel, double seconds)
        {
            ValidateChannel(channel);
            return GetLatestWindow(_rawBuffer[channel], seconds);
        }

        public float[] GetFilteredWindow(int channel, double seconds)
        {
            ValidateChannel(channel);
            return GetLatestWindow(_filteredBuffer[channel], seconds);
        }

        // 取所有通道最新 N 秒窗口，返回 [channel][sample]
        public float[][] GetFilteredWindowAllChannels(double seconds)
        {
            var result = new float[_channelCount][];
            for (int ch = 0; ch < _channelCount; ch++)
            {
                result[ch] = GetFilteredWindow(ch, seconds);
            }
            return result;
        }

        // 用滤波结果覆盖当前 filteredBuffer
        // filteredData 必须是“按时间顺序”的整段结果
        public void ReplaceFilteredChannel(int channel, float[] filteredData)
        {
            ValidateChannel(channel);
            if (filteredData == null) throw new ArgumentNullException(nameof(filteredData));
            if (filteredData.Length != _count)
                throw new ArgumentException("filteredData length must equal current buffer count.");

            int start = (_writeIndex - _count + _capacity) % _capacity;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _capacity;
                _filteredBuffer[channel][idx] = filteredData[i];
            }
        }

        private float[] GetOrderedSnapshot(float[] source)
        {
            var result = new float[_count];
            int start = (_writeIndex - _count + _capacity) % _capacity;

            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _capacity;
                result[i] = source[idx];
            }

            return result;
        }

        private float[] GetLatestWindow(float[] source, double seconds)
        {
            int windowSamples = (int)Math.Round(seconds * _sampleRate);
            if (windowSamples <= 0) return Array.Empty<float>();

            int actual = Math.Min(windowSamples, _count);
            var result = new float[actual];

            int start = (_writeIndex - actual + _capacity) % _capacity;

            for (int i = 0; i < actual; i++)
            {
                int idx = (start + i) % _capacity;
                result[i] = source[idx];
            }

            return result;
        }

        private void ValidateChannel(int channel)
        {
            if (channel < 0 || channel >= _channelCount)
                Logger.Debug("[MultiChannelRingBuffer][ValidateChannel]:通道数量不对~");
        }
        #endregion


    }
}
