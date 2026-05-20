
namespace FrameWork.Tools
{
    public static class MathUtil
    {
        public static double Clamp(double value, double min, double max)
            => (value < min) ? min : (value > max) ? max : value;

        public static int Clamp(int value, int min, int max)
            => (value < min) ? min : (value > max) ? max : value;
    }
}
