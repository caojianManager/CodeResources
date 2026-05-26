using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Resources
{
    public static class ResourcesImagePath
    {
        private static ImageSource Load(string relativePath) =>
                new BitmapImage(new Uri($"pack://application:,,,/EEGTool;component/{relativePath}", UriKind.Absolute));

        public static ImageSource WARNING_ICON = Load("/Resources/images/warning_icon.png");
        public static ImageSource SYSTEM_BG = Load("/Resources/images/system_bg.jpg");

        public static ImageSource COLLECTION = Load("/Resources/images/collection.png");
        public static ImageSource PLAYBACK = Load("/Resources/images/playback.png");
        public static ImageSource TEMPLATE = Load("/Resources/images/template.png");

    }
}
