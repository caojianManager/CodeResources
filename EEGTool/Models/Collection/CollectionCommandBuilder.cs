using System.Linq;
using EEGTool.Models.BLE;
using EEGTool.Models.Template;
using FrameWork.Log;

namespace EEGTool.Models.Collection
{
    public static class CollectionCommandBuilder
    {
        public static byte[] BuildConfigureCommand(CollectionInfo collectionInfo)
        {
            var channelList = TemplateFileManager.GetInstance()
                .GetCurrentChannelList(collectionInfo.Template)
                .Where(channel => channel >= 1 && channel <= CommandManager.ChannelCount)
                .Distinct()
                .ToList();

            if (channelList.Count == 0)
            {
                Logger.Info("[CollectionCommandBuilder][BuildConfigureCommand]:当前模板没有有效通道，默认开启16通道采集");
                channelList = Enumerable.Range(1, CommandManager.ChannelCount).ToList();
            }

            ushort channelMask = CommandManager.BuildChannelMask(channelList);
            ushort sampleRate = collectionInfo.SampleRate > 0 ? (ushort)collectionInfo.SampleRate : (ushort)250;
            ushort durationSeconds = collectionInfo.Template.Time > 0 ? (ushort)collectionInfo.Template.Time : (ushort)60;

            return CommandManager.BuildConfigureCollectionCommand(
                channelMask,
                sampleRate,
                durationSeconds);
        }
    }
}
