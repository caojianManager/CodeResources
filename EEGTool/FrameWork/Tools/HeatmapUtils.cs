using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FrameWork.Tools
{
    /// <summary>
    /// Heatmap 工具类，用于 EEG 头皮拓扑图生成
    /// 支持：
    /// 1. 电极值插值（RBF/高斯权重）
    /// 2. 可分离高斯模糊
    /// 3. 鲁棒 min/max 计算（百分位数）
    /// 4. 渲染 WriteableBitmap，支持 Jet 色图和 Viridis
    /// </summary>
    public static class HeatmapUtils
    {
        /// <summary>
        /// 生成网格值（RBF 高斯权重插值）
        /// points: 电极 2D 坐标，归一化到 [0,1] 范围
        /// values: 电极对应数值
        /// width, height: 输出网格大小
        /// </summary>
        public static double[,] GenerateGrid((double x, double y)[] points, double[] values, int width, int height)
        {
            int w = width, h = height;
            double[,] grid = new double[w, h];
            double sigma = 0.12; // 插值高斯权重的空间 sigma（归一化坐标）

            // 过滤非有限点/值，避免 NaN 传播
            var valid = new List<(double x, double y, double v)>();
            int count = Math.Min(points.Length, values.Length);
            for (int i = 0; i < count; i++)
            {
                var px = points[i].x;
                var py = points[i].y;
                var v = values[i];
                if (double.IsNaN(px) || double.IsInfinity(px)) continue;
                if (double.IsNaN(py) || double.IsInfinity(py)) continue;
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                valid.Add((px, py, v));
            }

            for (int yi = 0; yi < h; yi++)
            {
                // 将网格 y 映射到 [0,1]，顶部 y=1
                double y = 1.0 - (double)yi / (h - 1);
                for (int xi = 0; xi < w; xi++)
                {
                    double x = (double)xi / (w - 1);

                    // RBF 插值
                    if (valid.Count == 0)
                    {
                        grid[xi, yi] = 0.0;
                        continue;
                    }

                    double num = 0, den = 0;
                    for (int k = 0; k < valid.Count; k++)
                    {
                        double dx = x - valid[k].x;
                        double dy = y - valid[k].y;
                        double dist2 = dx * dx + dy * dy;
                        double wgt = Math.Exp(-dist2 / (2 * sigma * sigma));
                        num += wgt * valid[k].v;
                        den += wgt;
                    }
                    grid[xi, yi] = den > 0 ? num / den : 0.0;
                }
            }

            return grid;
        }

        /// <summary>
        /// 可分离高斯模糊，对 NaN 自动忽略
        /// sigma: 像素级高斯模糊半径
        /// </summary>
        public static double[,] GaussianBlurSeparable(double[,] data, double sigma)
        {
            if (sigma <= 0) return data;

            int w = data.GetLength(0), h = data.GetLength(1);

            // 生成一维高斯核
            int radius = Math.Max(1, (int)Math.Ceiling(3 * sigma));
            int size = radius * 2 + 1;
            double[] kernel = new double[size];
            double sum = 0;
            for (int i = -radius; i <= radius; i++)
            {
                double v = Math.Exp(-(i * i) / (2 * sigma * sigma));
                kernel[i + radius] = v;
                sum += v;
            }
            for (int i = 0; i < size; i++) kernel[i] /= sum;

            double[,] temp = new double[w, h];

            // 水平方向模糊
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double s = 0, wsum = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int ix = x + k;
                        if (ix < 0 || ix >= w) continue;
                        double v = data[ix, y];
                        if (double.IsNaN(v)) continue;
                        s += v * kernel[k + radius];
                        wsum += kernel[k + radius];
                    }
                    temp[x, y] = wsum > 0 ? s / wsum : double.NaN;
                }
            }

            double[,] result = new double[w, h];

            // 垂直方向模糊
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    double s = 0, wsum = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int iy = y + k;
                        if (iy < 0 || iy >= h) continue;
                        double v = temp[x, iy];
                        if (double.IsNaN(v)) continue;
                        s += v * kernel[k + radius];
                        wsum += kernel[k + radius];
                    }
                    result[x, y] = wsum > 0 ? s / wsum : double.NaN;
                }
            }

            return result;
        }

        /// <summary>
        /// 鲁棒计算网格 min/max（排除极值），默认 2%-98% 百分位
        /// </summary>
        public static (double min, double max) RobustMinMax(double[,] grid, double lowPercent = 2.0, double highPercent = 98.0)
        {
            var list = new List<double>();
            int w = grid.GetLength(0), h = grid.GetLength(1);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var v = grid[x, y];
                    if (!double.IsNaN(v)) list.Add(v);
                }

            if (list.Count < 5) return (0.0, 1.0); // 数据太少时返回默认

            list.Sort();
            double min = Percentile(list, lowPercent);
            double max = Percentile(list, highPercent);

            if (min == max)
            {
                min = list.First();
                max = list.Last();
                if (min == max) max = min + 1e-6;
            }

            return (min, max);
        }

        private static double Percentile(List<double> sorted, double pct)
        {
            if (pct <= 0) return sorted[0];
            if (pct >= 100) return sorted[^1];
            double pos = (pct / 100.0) * (sorted.Count - 1);
            int idx = (int)Math.Floor(pos);
            double frac = pos - idx;
            return (idx + 1 < sorted.Count) ? sorted[idx] * (1 - frac) + sorted[idx + 1] * frac : sorted[idx];
        }

        /// <summary>
        /// 将网格渲染成 WriteableBitmap
        /// minVal/maxVal: 映射区间
        /// gamma: 亮度调节
        /// useJet: 是否使用 Jet 色图
        /// </summary>
        public static WriteableBitmap GridToBitmap(double[,] grid, double minVal, double maxVal, double gamma = 0.6, bool useJet = true)
        {
            int w = grid.GetLength(0), h = grid.GetLength(1);
            WriteableBitmap bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];

            double span = maxVal - minVal;
            if (span <= 1e-9) span = 1.0;

            // 可选：生成 256 色 LUT，提高性能
            (byte r, byte g, byte b)[] lut = new (byte, byte, byte)[256];
            for (int i = 0; i < 256; i++)
            {
                double t = i / 255.0;
                var c = useJet ? Jet(t) : ViridisApprox(t);
                lut[i] = ((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255));
            }

            for (int yi = 0; yi < h; yi++)
            {
                for (int xi = 0; xi < w; xi++)
                {
                    int idx = yi * stride + xi * 4;
                    double val = grid[xi, yi];
                    if (double.IsNaN(val))
                    {
                        pixels[idx + 3] = 0;
                        continue;
                    }

                    double norm = (val - minVal) / span;
                    norm = Math.Max(0, Math.Min(1, norm));
                    norm = Math.Pow(norm, gamma);

                    int colorIdx = (int)(norm * 255);
                    var c = lut[colorIdx];
                    pixels[idx + 0] = c.b;
                    pixels[idx + 1] = c.g;
                    pixels[idx + 2] = c.r;
                    pixels[idx + 3] = 255;
                }
            }

            bmp.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
            return bmp;
        }

        /// <summary>
        /// Jet 色图（蓝-青-绿-黄-红）
        /// </summary>
        private static (double r, double g, double b) Jet(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            double r = Math.Max(0, Math.Min(1, 1.5 - Math.Abs(4 * t - 3)));
            double g = Math.Max(0, Math.Min(1, 1.5 - Math.Abs(4 * t - 2)));
            double b = Math.Max(0, Math.Min(1, 1.5 - Math.Abs(4 * t - 1)));
            return (r, g, b);
        }

        /// <summary>
        /// Viridis 近似色图
        /// </summary>
        private static (double r, double g, double b) ViridisApprox(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return (0.280 * Math.Pow(1 - t, 0.5) + 0.481 * t,
                    0.395 * (1 - t) + 0.772 * t,
                    0.570 * (1 - t) + 0.366 * t);
        }
    }
}
