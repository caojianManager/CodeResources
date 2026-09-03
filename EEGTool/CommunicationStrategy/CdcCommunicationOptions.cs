using BrainZoneMultichannel.FrameWork.NRF52840;

namespace EEGTool.CommunicationStrategy
{
    public sealed class CdcCommunicationOptions : CommunicationOptions
    {
        public CdcCommunicationOptions(string portName)
        {
            Options = new CDCOptions(portName);
        }

        public override CommunicationType Type => CommunicationType.Cdc;

        public CDCOptions Options { get; }
    }
}
