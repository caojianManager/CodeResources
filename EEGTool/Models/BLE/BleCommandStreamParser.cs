using System;
using System.Collections.Generic;
using System.Linq;

namespace EEGTool.Models.BLE
{
    public sealed class BleCommandStreamParser
    {
        private const int MinimumFrameLength = 5;
        private readonly List<byte> _buffer = new();

        public int MaxBufferSize { get; set; } = 64 * 1024;
        public int BufferedCount => _buffer.Count;

        public IReadOnlyList<CommandParseResult> Push(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return Array.Empty<CommandParseResult>();
            }

            _buffer.AddRange(data);
            TrimOverflow();
            return ParseBufferedFrames();
        }

        public void Clear()
        {
            _buffer.Clear();
        }

        private IReadOnlyList<CommandParseResult> ParseBufferedFrames()
        {
            var results = new List<CommandParseResult>();

            while (_buffer.Count >= MinimumFrameLength)
            {
                int headerIndex = _buffer.IndexOf(CommandManager.Header);
                if (headerIndex < 0)
                {
                    _buffer.Clear();
                    break;
                }

                if (headerIndex > 0)
                {
                    _buffer.RemoveRange(0, headerIndex);
                }

                if (_buffer.Count < MinimumFrameLength)
                {
                    break;
                }

                if (!Enum.IsDefined(typeof(BleCommandType), _buffer[1]))
                {
                    results.Add(CommandParseResult.Fail(
                        CommandParseStatus.UnsupportedCommand,
                        $"Unsupported command type 0x{_buffer[1]:X2}."));
                    _buffer.RemoveAt(0);
                    continue;
                }

                var commandType = (BleCommandType)_buffer[1];
                int frameLength = TryGetFrameLength(commandType);
                if (frameLength <= 0)
                {
                    results.Add(CommandParseResult.Fail(
                        CommandParseStatus.UnsupportedCommand,
                        $"Unsupported command type 0x{_buffer[1]:X2}."));
                    _buffer.RemoveAt(0);
                    continue;
                }

                if (_buffer.Count < frameLength)
                {
                    break;
                }

                byte[] frameBytes = _buffer.Take(frameLength).ToArray();
                _buffer.RemoveRange(0, frameLength);

                var result = CommandManager.Parse(frameBytes);
                results.Add(result);
            }

            return results;
        }

        private int TryGetFrameLength(BleCommandType commandType)
        {
            if (UsesTwoByteLength(commandType))
            {
                if (_buffer.Count < 4)
                {
                    return 0;
                }

                ushort length = (ushort)(_buffer[2] | (_buffer[3] << 8));
                return length + 3;
            }

            return commandType switch
            {
                BleCommandType.ConfigureCollectionResponse => 7,
                BleCommandType.StopCollectionResponse => 7,
                BleCommandType.ConfigureStimulusResponse => 7,
                BleCommandType.StopStimulusResponse => 6,
                BleCommandType.ConfigureImpedanceResponse => 7,
                BleCommandType.BatteryResponse => 6,
                BleCommandType.StopImpedanceResponse => 6,
                _ => 5
            };
        }

        private static bool UsesTwoByteLength(BleCommandType commandType)
        {
            return commandType == BleCommandType.CollectionData ||
                   commandType == BleCommandType.ImpedanceMonitorData;
        }

        private void TrimOverflow()
        {
            if (_buffer.Count <= MaxBufferSize)
            {
                return;
            }

            int headerIndex = _buffer.IndexOf(CommandManager.Header);
            if (headerIndex > 0)
            {
                _buffer.RemoveRange(0, headerIndex);
            }

            if (_buffer.Count > MaxBufferSize)
            {
                _buffer.Clear();
            }
        }
    }
}
