using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EEGTool.Models.BLE
{
    public enum BleCommandType : byte
    {
        ConfigureCollection = 0x01,
        StartCollection = 0x02,
        StopCollection = 0x03,
        ConfigureStimulus = 0x04,
        StartStimulus = 0x05,
        StopStimulus = 0x06,
        ConfigureImpedance = 0x07,
        QueryBattery = 0x08,
        StopImpedance = 0x09,
        StartImpedanceMonitor = 0x10,
        ConfigureCollectionResponse = 0x81,
        CollectionData = 0x82,
        StopCollectionResponse = 0x83,
        ConfigureStimulusResponse = 0x84,
        StopStimulusResponse = 0x86,
        ImpedanceData = 0x87,
        BatteryResponse = 0x88,
        StopImpedanceResponse = 0x89,
        ImpedanceMonitorData = 0x90
    }

    public enum CommandParseStatus
    {
        Success,
        EmptyData,
        HeaderNotFound,
        IncompleteFrame,
        InvalidHeader,
        InvalidFooter,
        InvalidLength,
        InvalidChecksum,
        UnsupportedCommand
    }

    public enum CommandResponseStatus : byte
    {
        Success = 0x00,
        ParameterError = 0x01,
        ChecksumFailed = 0x02,
        Unsupported = 0x03
    }

    public enum StimulusMode : byte
    {
        Ac = 0x01,
        Dc = 0x02,
        Noise = 0x03
    }

    public enum StimulusPolarity : byte
    {
        Cathode = 0x01,
        Anode = 0x02
    }

    public enum StimulusLoopMode : byte
    {
        Stimulus = 0x01,
        Loop = 0x02
    }

    public sealed class CommandFrame
    {
        public CommandFrame(BleCommandType commandType, byte length, byte[] payload, byte checksum, byte[] rawBytes)
        {
            CommandType = commandType;
            Length = length;
            Payload = new ReadOnlyCollection<byte>(payload);
            Checksum = checksum;
            RawBytes = new ReadOnlyCollection<byte>(rawBytes);
        }

        public BleCommandType CommandType { get; }
        public byte Length { get; }
        public IReadOnlyList<byte> Payload { get; }
        public byte Checksum { get; }
        public IReadOnlyList<byte> RawBytes { get; }
    }

    public sealed class CommandResponse
    {
        public CommandResponse(BleCommandType commandType, byte statusCode, byte errorDetail, byte[] rawBytes)
        {
            CommandType = commandType;
            StatusCode = statusCode;
            ErrorDetail = errorDetail;
            RawBytes = new ReadOnlyCollection<byte>(rawBytes);
        }

        public BleCommandType CommandType { get; }
        public byte StatusCode { get; }
        public byte ErrorDetail { get; }
        public bool IsSuccess => StatusCode == (byte)CommandResponseStatus.Success;
        public IReadOnlyList<byte> RawBytes { get; }
    }

    public sealed class BatteryResponse
    {
        public BatteryResponse(byte electricityQuantity, byte[] rawBytes)
        {
            ElectricityQuantity = electricityQuantity;
            RawBytes = new ReadOnlyCollection<byte>(rawBytes);
        }

        public byte ElectricityQuantity { get; }
        public IReadOnlyList<byte> RawBytes { get; }
    }

    public sealed class DataFrame
    {
        public DataFrame(
            BleCommandType commandType,
            ushort length,
            byte electricityQuantity,
            ushort channelMask,
            byte channelCount,
            ushort sampleCount,
            byte[] rawData,
            int[][] samples,
            byte[] rawBytes)
        {
            CommandType = commandType;
            Length = length;
            ElectricityQuantity = electricityQuantity;
            ChannelMask = channelMask;
            ChannelCount = channelCount;
            SampleCount = sampleCount;
            RawData = new ReadOnlyCollection<byte>(rawData);
            Samples = new ReadOnlyCollection<int[]>(samples);
            RawBytes = new ReadOnlyCollection<byte>(rawBytes);
        }

        public BleCommandType CommandType { get; }
        public ushort Length { get; }
        public byte ElectricityQuantity { get; }
        public ushort ChannelMask { get; }
        public byte ChannelCount { get; }
        public ushort SampleCount { get; }
        public IReadOnlyList<byte> RawData { get; }
        public IReadOnlyList<int[]> Samples { get; }
        public IReadOnlyList<byte> RawBytes { get; }
    }

    public sealed class CommandParseResult
    {
        private CommandParseResult(CommandParseStatus status, string message, CommandFrame? frame, CommandResponse? response, BatteryResponse? battery, DataFrame? dataFrame)
        {
            Status = status;
            Message = message;
            Frame = frame;
            Response = response;
            Battery = battery;
            DataFrame = dataFrame;
        }

        public CommandParseStatus Status { get; }
        public bool IsSuccess => Status == CommandParseStatus.Success;
        public string Message { get; }
        public CommandFrame? Frame { get; }
        public CommandResponse? Response { get; }
        public BatteryResponse? Battery { get; }
        public DataFrame? DataFrame { get; }

        public static CommandParseResult Success(CommandFrame frame, CommandResponse? response = null, BatteryResponse? battery = null, DataFrame? dataFrame = null)
        {
            return new CommandParseResult(CommandParseStatus.Success, string.Empty, frame, response, battery, dataFrame);
        }

        public static CommandParseResult Fail(CommandParseStatus status, string message)
        {
            return new CommandParseResult(status, message, null, null, null, null);
        }
    }

    public sealed class CommandManager
    {
        public const byte Header = 0xAA;
        public const byte Footer = 0x55;
        public const int ChannelCount = 16;

        public static byte[] BuildConfigureCollectionCommand(ushort channelMask, ushort sampleRate, ushort durationSeconds)
        {
            return BuildPayloadCommand(
                BleCommandType.ConfigureCollection,
                WriteUInt16(channelMask)
                    .Concat(WriteUInt16(sampleRate))
                    .Concat(WriteUInt16(durationSeconds))
                    .ToArray());
        }

        public static byte[] BuildStartCollectionCommand()
        {
            return BuildLengthOnlyCommand(BleCommandType.StartCollection);
        }

        public static byte[] BuildStopCollectionCommand()
        {
            return BuildLengthOnlyCommand(BleCommandType.StopCollection);
        }

        public static byte[] BuildConfigureStimulusCommand(
            StimulusMode stimulusMode,
            ushort frequency,
            StimulusPolarity polarity,
            StimulusLoopMode loopMode,
            byte channelId,
            ushort durationMilliseconds,
            ushort electricityMicroAmpere)
        {
            ValidateRange(channelId, 1, ChannelCount, nameof(channelId));

            return BuildPayloadCommand(
                BleCommandType.ConfigureStimulus,
                new[]
                {
                    (byte)stimulusMode
                }
                .Concat(WriteUInt16(frequency))
                .Concat(new[] { (byte)polarity, (byte)loopMode, channelId })
                .Concat(WriteUInt16(durationMilliseconds))
                .Concat(WriteUInt16(electricityMicroAmpere))
                .ToArray());
        }

        public static byte[] BuildStartStimulusCommand()
        {
            return BuildLengthOnlyCommand(BleCommandType.StartStimulus);
        }

        public static byte[] BuildStopStimulusCommand()
        {
            return BuildSpecialChecksumCommand(BleCommandType.StopStimulus, (byte)BleCommandType.StopStimulus);
        }

        public static byte[] BuildConfigureImpedanceCommand(ushort channelMask, ushort sampleRate, ushort durationSeconds)
        {
            return BuildPayloadCommand(
                BleCommandType.ConfigureImpedance,
                WriteUInt16(channelMask)
                    .Concat(WriteUInt16(sampleRate))
                    .Concat(WriteUInt16(durationSeconds))
                    .ToArray());
        }

        public static byte[] BuildQueryBatteryCommand()
        {
            return BuildLengthOnlyCommand(BleCommandType.QueryBattery);
        }

        public static byte[] BuildStopImpedanceCommand()
        {
            return BuildSpecialChecksumCommand(BleCommandType.StopImpedance, (byte)BleCommandType.StopImpedance);
        }

        public static byte[] BuildStartImpedanceMonitorCommand()
        {
            return BuildLengthOnlyCommand(BleCommandType.StartImpedanceMonitor);
        }

        public static ushort BuildChannelMask(IEnumerable<int> channels)
        {
            if (channels == null)
            {
                throw new ArgumentNullException(nameof(channels));
            }

            ushort mask = 0;
            foreach (int channel in channels)
            {
                ValidateRange(channel, 1, ChannelCount, nameof(channels));
                mask |= (ushort)(1 << (channel - 1));
            }

            return mask;
        }

        public static IReadOnlyList<int> GetEnabledChannels(ushort channelMask)
        {
            var channels = new List<int>();
            for (int index = 0; index < ChannelCount; index++)
            {
                if ((channelMask & (1 << index)) != 0)
                {
                    channels.Add(index + 1);
                }
            }

            return channels;
        }

        public static CommandParseResult Parse(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return CommandParseResult.Fail(CommandParseStatus.EmptyData, "Data is empty.");
            }

            int headerIndex = Array.IndexOf(data, Header);
            if (headerIndex < 0)
            {
                return CommandParseResult.Fail(CommandParseStatus.HeaderNotFound, "Header 0xAA was not found.");
            }

            if (headerIndex > 0)
            {
                data = data.Skip(headerIndex).ToArray();
            }

            if (data.Length < 5)
            {
                return CommandParseResult.Fail(CommandParseStatus.IncompleteFrame, "Frame is shorter than the minimum command length.");
            }

            if (data[0] != Header)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidHeader, "Invalid frame header.");
            }

            if (!Enum.IsDefined(typeof(BleCommandType), data[1]))
            {
                return CommandParseResult.Fail(CommandParseStatus.UnsupportedCommand, $"Unsupported command type 0x{data[1]:X2}.");
            }

            var commandType = (BleCommandType)data[1];
            return UsesTwoByteLength(commandType)
                ? ParseTwoByteLengthFrame(data, commandType)
                : ParseOneByteLengthFrame(data, commandType);
        }

        public static IReadOnlyList<CommandParseResult> ParseMany(byte[] data)
        {
            var results = new List<CommandParseResult>();
            if (data == null || data.Length == 0)
            {
                results.Add(CommandParseResult.Fail(CommandParseStatus.EmptyData, "Data is empty."));
                return results;
            }

            int offset = 0;
            while (offset < data.Length)
            {
                int headerIndex = Array.IndexOf(data, Header, offset);
                if (headerIndex < 0)
                {
                    break;
                }

                int footerIndex = Array.IndexOf(data, Footer, headerIndex + 1);
                if (footerIndex < 0)
                {
                    results.Add(CommandParseResult.Fail(CommandParseStatus.IncompleteFrame, "Frame footer was not found."));
                    break;
                }

                byte[] frameBytes = data.Skip(headerIndex).Take(footerIndex - headerIndex + 1).ToArray();
                results.Add(Parse(frameBytes));
                offset = footerIndex + 1;
            }

            if (results.Count == 0)
            {
                results.Add(CommandParseResult.Fail(CommandParseStatus.HeaderNotFound, "Header 0xAA was not found."));
            }

            return results;
        }

        public static byte CalculateChecksum(IEnumerable<byte> bytes)
        {
            byte checksum = 0;
            foreach (byte item in bytes)
            {
                checksum ^= item;
            }

            return checksum;
        }

        public static string ToHexString(IEnumerable<byte> data)
        {
            return data == null ? string.Empty : string.Join(" ", data.Select(item => item.ToString("X2")));
        }

        private static byte[] BuildPayloadCommand(BleCommandType commandType, byte[] payload)
        {
            byte length = checked((byte)(payload.Length + 2));
            return BuildCommand(commandType, length, payload, CalculateChecksum(new[] { length }.Concat(payload)));
        }

        private static byte[] BuildLengthOnlyCommand(BleCommandType commandType)
        {
            const byte protocolFixedLength = 0x04;
            return BuildCommand(commandType, protocolFixedLength, Array.Empty<byte>(), protocolFixedLength);
        }

        private static byte[] BuildSpecialChecksumCommand(BleCommandType commandType, byte checksum)
        {
            const byte protocolFixedLength = 0x04;
            return BuildCommand(commandType, protocolFixedLength, Array.Empty<byte>(), checksum);
        }

        private static byte[] BuildCommand(BleCommandType commandType, byte length, byte[] payload, byte checksum)
        {
            var frame = new byte[payload.Length + 5];
            frame[0] = Header;
            frame[1] = (byte)commandType;
            frame[2] = length;
            Array.Copy(payload, 0, frame, 3, payload.Length);
            frame[^2] = checksum;
            frame[^1] = Footer;
            return frame;
        }

        private static CommandParseResult ParseOneByteLengthFrame(byte[] data, BleCommandType commandType)
        {
            if (data[^1] != Footer)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidFooter, "Invalid frame footer.");
            }

            byte length = data[2];
            int payloadLength = data.Length - 5;
            if (payloadLength < 0)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidLength, "Invalid frame length.");
            }

            byte[] payload = data.Skip(3).Take(payloadLength).ToArray();
            byte checksum = data[^2];
            byte expectedChecksum = CalculateChecksum(new[] { length }.Concat(payload));
            if (checksum != expectedChecksum)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidChecksum, $"Checksum mismatch, expected 0x{expectedChecksum:X2}, actual 0x{checksum:X2}.");
            }

            var frame = new CommandFrame(commandType, length, payload, checksum, data);
            if (commandType == BleCommandType.BatteryResponse)
            {
                if (payload.Length < 1)
                {
                    return CommandParseResult.Fail(CommandParseStatus.InvalidLength, "Battery response payload is missing electricity quantity.");
                }

                return CommandParseResult.Success(frame, battery: new BatteryResponse(payload[0], data));
            }

            if (IsStatusResponse(commandType))
            {
                if (payload.Length < 1)
                {
                    return CommandParseResult.Fail(CommandParseStatus.InvalidLength, "Status response payload is missing status code.");
                }

                byte errorDetail = payload.Length > 1 ? payload[1] : (byte)0;
                return CommandParseResult.Success(frame, response: new CommandResponse(commandType, payload[0], errorDetail, data));
            }

            return CommandParseResult.Success(frame);
        }

        private static CommandParseResult ParseTwoByteLengthFrame(byte[] data, BleCommandType commandType)
        {
            if (data.Length < 12)
            {
                return CommandParseResult.Fail(CommandParseStatus.IncompleteFrame, "Data frame is shorter than the minimum data length.");
            }

            if (data[^1] != Footer)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidFooter, "Invalid frame footer.");
            }

            ushort length = ReadUInt16(data, 2);
            int expectedTotalLength = length + 3;
            if (expectedTotalLength != data.Length)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidLength, $"Frame length mismatch, expected {expectedTotalLength}, actual {data.Length}.");
            }

            int payloadLength = data.Length - 6;
            byte[] payload = data.Skip(4).Take(payloadLength).ToArray();
            byte checksum = data[^2];
            byte expectedChecksum = CalculateChecksum(data.Skip(2).Take(data.Length - 4));
            if (checksum != expectedChecksum)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidChecksum, $"Checksum mismatch, expected 0x{expectedChecksum:X2}, actual 0x{checksum:X2}.");
            }

            byte electricityQuantity = payload[0];
            ushort channelMask = ReadUInt16(payload, 1);
            byte channelCount = payload[3];
            ushort sampleCount = ReadUInt16(payload, 4);
            byte[] rawData = payload.Skip(6).ToArray();
            int expectedRawDataLength = channelCount * sampleCount * 3;
            if (rawData.Length != expectedRawDataLength)
            {
                return CommandParseResult.Fail(CommandParseStatus.InvalidLength, $"Data area length mismatch, expected {expectedRawDataLength}, actual {rawData.Length}.");
            }

            var frame = new CommandFrame(commandType, 0, payload, checksum, data);
            var samples = ParseThreeByteSamples(rawData, channelCount, sampleCount);
            var dataFrame = new DataFrame(commandType, length, electricityQuantity, channelMask, channelCount, sampleCount, rawData, samples, data);
            return CommandParseResult.Success(frame, dataFrame: dataFrame);
        }

        private static bool UsesTwoByteLength(BleCommandType commandType)
        {
            return commandType == BleCommandType.CollectionData ||
                   commandType == BleCommandType.ImpedanceData ||
                   commandType == BleCommandType.ImpedanceMonitorData;
        }

        private static bool IsStatusResponse(BleCommandType commandType)
        {
            return commandType == BleCommandType.ConfigureCollectionResponse ||
                   commandType == BleCommandType.StopCollectionResponse ||
                   commandType == BleCommandType.ConfigureStimulusResponse ||
                   commandType == BleCommandType.StopStimulusResponse ||
                   commandType == BleCommandType.StopImpedanceResponse;
        }

        private static byte[] WriteUInt16(ushort value)
        {
            return new[] { (byte)(value & 0xFF), (byte)(value >> 8) };
        }

        private static ushort ReadUInt16(IReadOnlyList<byte> data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static int[][] ParseThreeByteSamples(byte[] rawData, int channelCount, int sampleCount)
        {
            var samples = new int[channelCount][];
            for (int channel = 0; channel < channelCount; channel++)
            {
                samples[channel] = new int[sampleCount];
                for (int sample = 0; sample < sampleCount; sample++)
                {
                    int index = (channel * sampleCount + sample) * 3;
                    int value = (rawData[index] << 16) | (rawData[index + 1] << 8) | rawData[index + 2];
                    if ((value & 0x800000) != 0)
                    {
                        value |= unchecked((int)0xFF000000);
                    }

                    samples[channel][sample] = value;
                }
            }

            return samples;
        }

        private static void ValidateRange(int value, int min, int max, string paramName)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max}.");
            }
        }
    }
}
