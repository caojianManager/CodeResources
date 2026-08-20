using RJCP.IO.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrainZoneMultichannel.FrameWork.NRF52840
{
    public sealed class CDCOptions
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; } = 115200;
        public int DataBits { get; set; } = 8;
        public Parity Parity { get; set; } = Parity.None;
        public StopBits StopBits { get; set; } = StopBits.One;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int ReadBufferSize { get; set; } = 64 * 1024;
        public int WriteBufferSize { get; set; } = 16 * 1024;
        public int ReceiveBufferSize { get; set; } = 4096;
        public int ReceiveQueueCapacity { get; set; } = 256;
        public bool DropOldestWhenReceiveQueueFull { get; set; }
        public bool DtrEnable { get; set; } = true;
        public bool RtsEnable { get; set; }
        public Encoding TextEncoding { get; set; } = Encoding.UTF8;

        public CDCOptions(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
            {
                throw new ArgumentException("串口名称不能为空", nameof(portName));
            }

            PortName = portName;
        }
    }
}
