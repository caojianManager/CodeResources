using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace FrameWork.Common
{
    public class ImageSourceExtension
    {
        //注-指纹的采集.raw 的尺寸是256*360
        public static ImageSource ConvertRawToImageSource(byte[] rawData, int width, int height)
        {
            var bitmap = new WriteableBitmap(
                width, height, 96, 96, PixelFormats.Gray8, null);

            bitmap.WritePixels(
                new System.Windows.Int32Rect(0, 0, width, height),
                rawData, width, 0);

            return bitmap;
        }
    }
}
