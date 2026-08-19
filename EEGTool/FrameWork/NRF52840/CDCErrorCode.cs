using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrainZoneMultichannel.FrameWork.NRF52840
{
    public enum CDCErrorCode
    {
        OpenFailed = 0,
        CloseFailed = 1,
        SendFailed = 2,
        ReceiveFailed = 3,
        PortNotOpen = 4,
        PortAlreadyOpen = 5
    }
}
