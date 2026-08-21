using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EEGTool.FrameWork.MediaFoundation
{
    public sealed class CameraFrame
    {
        public CameraFrame(byte[] data, int width, int height, long timestamp)
        {
            Data = data;
            Width = width;
            Height = height;
            Timestamp = timestamp;
        }

        /// <summary>
        /// BGRA / RGB32 数据，每个像素 4 字节
        /// 顺序一般是 B, G, R, A
        /// </summary>
        public byte[] Data { get; }

        public int Width { get; }

        public int Height { get; }

        public long Timestamp { get; }
    }
}
