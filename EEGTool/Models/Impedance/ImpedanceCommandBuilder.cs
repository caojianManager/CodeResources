using System.Linq;
using EEGTool.Models.BLE;
using EEGTool.Models.Collection;
using EEGTool.Models.Template;
using FrameWork.Log;

namespace EEGTool.Models.Impedance
{
    public static class ImpedanceCommandBuilder
    {
        public static byte[] BuildConfigureCommand(CollectionInfo collectionInfo)
        {
            var channels = TemplateFileManager.GetInstance()
                .GetCurrentChannelList(collectionInfo.Template)
                .Where(channel => channel >= 1 && channel <= CommandManager.ChannelCount)
                .Distinct()
                .ToList();

            if (channels.Count == 0)
            {
                Logger.Info("[ImpedanceCommandBuilder][BuildConfigureCommand]:当前模板没有有效通道，默认开启16通道阻抗监测");
                channels = Enumerable.Range(1, CommandManager.ChannelCount).ToList();
            }

            ushort channelMask = CommandManager.BuildChannelMask(channels);
            ushort sampleRate = collectionInfo.SampleRate > 0
                ? checked((ushort)collectionInfo.SampleRate)
                : (ushort)250;
            ushort durationSeconds = collectionInfo.Template.Time > 0
                ? checked((ushort)collectionInfo.Template.Time)
                : (ushort)60;

            return CommandManager.BuildConfigureImpedanceCommand(
                channelMask,
                sampleRate,
                durationSeconds);
        }
    }
}
