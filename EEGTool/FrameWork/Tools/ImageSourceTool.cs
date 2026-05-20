using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FrameWork.Tools
{
    public static class ImageSourceTool
    {
        public static ImageSource ByteArrayToImageSource(byte[] imageData)
        {
            using (var ms = new System.IO.MemoryStream(imageData))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 读取完就关闭流
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // 避免跨线程问题
                return bitmap;
            }
        }
    }
}
