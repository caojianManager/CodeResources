using System.Globalization;
using System.Media;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace StockMonitorWpf;

public partial class MainWindow
{
    private readonly DispatcherTimer _timer = new();
    private readonly HttpClient _httpClient;
    private bool _loading;
    private bool _upperAlerted;
    private bool _lowerAlerted;
    private string _monitorCode = "002241";

    public MainWindow()
    {
        InitializeComponent();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        _timer.Tick += async (_, _) => await RefreshQuoteAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _httpClient.Dispose();
        base.OnClosed(e);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadInputs(out var code, out var upper, out var lower, out var intervalSeconds))
        {
            return;
        }

        _monitorCode = code;
        _upperAlerted = false;
        _lowerAlerted = false;
        _timer.Interval = TimeSpan.FromSeconds(intervalSeconds);
        _timer.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        CodeBox.IsEnabled = false;
        IntervalBox.IsEnabled = false;
        HeaderStatusText.Text = "监测中";
        ThresholdText.Text = $"{upper:0.###} / {lower:0.###}";

        AddLog($"开始监测 {code}，上限 {upper:0.###}，下限 {lower:0.###}");
        await RefreshQuoteAsync();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        CodeBox.IsEnabled = true;
        IntervalBox.IsEnabled = true;
        HeaderStatusText.Text = "已停止";
        AddLog("停止监测");
    }

    private bool TryReadInputs(out string code, out decimal upper, out decimal lower, out int intervalSeconds)
    {
        code = CodeBox.Text.Trim();
        upper = 0;
        lower = 0;
        intervalSeconds = 3;

        if (!StockCode.IsValid(code))
        {
            MessageBox.Show("请输入 6 位 A 股代码，例如 002241。", "代码错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!decimal.TryParse(UpperBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out upper) || upper <= 0)
        {
            MessageBox.Show("上限价格格式不正确。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!decimal.TryParse(LowerBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out lower) || lower <= 0)
        {
            MessageBox.Show("下限价格格式不正确。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (lower >= upper)
        {
            MessageBox.Show("下限价格必须小于上限价格。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(IntervalBox.Text.Trim(), out intervalSeconds) || intervalSeconds < 1)
        {
            MessageBox.Show("刷新秒数必须大于等于 1。", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        intervalSeconds = Math.Clamp(intervalSeconds, 1, 3600);
        return true;
    }

    private async Task RefreshQuoteAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        try
        {
            HeaderStatusText.Text = "刷新中";
            var quote = await TencentQuoteClient.GetQuoteAsync(_httpClient, _monitorCode)
                ?? await EastMoneyQuoteClient.GetQuoteAsync(_httpClient, _monitorCode);

            if (quote is null)
            {
                HeaderStatusText.Text = "无行情";
                AddLog("没有获取到行情");
                return;
            }

            RenderQuote(quote);
            CheckAlerts(quote);
            HeaderStatusText.Text = $"已刷新 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            HeaderStatusText.Text = "刷新失败";
            AddLog("刷新失败：" + ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void RenderQuote(StockQuote quote)
    {
        var quoteBrush = quote.Change >= 0
            ? (Brush)FindResource("GainBrush")
            : (Brush)FindResource("LossBrush");

        CodeText.Text = quote.Code;
        PriceText.Text = quote.Price.ToString("0.###", CultureInfo.InvariantCulture);
        PriceText.Foreground = quoteBrush;
        ChangeText.Text = $"{quote.Change:+0.###;-0.###;0}  {quote.ChangePercent:+0.##;-0.##;0}%";
        ChangeText.Foreground = quoteBrush;
        RangeText.Text = $"{quote.High:0.###} / {quote.Low:0.###}";
        OpenCloseText.Text = $"{quote.Open:0.###} / {quote.PreClose:0.###}";

        AddLog($"{quote.Code} 当前 {quote.Price:0.###} 涨跌 {quote.Change:+0.###;-0.###;0}");
    }

    private void CheckAlerts(StockQuote quote)
    {
        var upper = decimal.Parse(UpperBox.Text.Trim(), CultureInfo.InvariantCulture);
        var lower = decimal.Parse(LowerBox.Text.Trim(), CultureInfo.InvariantCulture);

        if (quote.Price >= upper && !_upperAlerted)
        {
            Alert($"价格达到上限 {upper:0.###}，当前 {quote.Price:0.###}");
            _upperAlerted = true;
        }
        else if (quote.Price < upper)
        {
            _upperAlerted = false;
        }

        if (quote.Price <= lower && !_lowerAlerted)
        {
            Alert($"价格达到下限 {lower:0.###}，当前 {quote.Price:0.###}");
            _lowerAlerted = true;
        }
        else if (quote.Price > lower)
        {
            _lowerAlerted = false;
        }
    }

    private void Alert(string message)
    {
        SystemSounds.Exclamation.Play();
        AddLog("提醒：" + message);
        MessageBox.Show(message, "价格提醒", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AddLog(string message)
    {
        LogList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (LogList.Items.Count > 200)
        {
            LogList.Items.RemoveAt(LogList.Items.Count - 1);
        }
    }
}

internal static class TencentQuoteClient
{
    public static async Task<StockQuote?> GetQuoteAsync(HttpClient httpClient, string code)
    {
        var stockCode = StockCode.Normalize(code);
        var marketCode = stockCode.SecId.StartsWith("1.", StringComparison.Ordinal) ? "sh" : "sz";
        var bytes = await httpClient.GetByteArrayAsync($"http://qt.gtimg.cn/q={marketCode}{stockCode.PureCode}");
        var content = Encoding.Latin1.GetString(bytes);
        var firstQuote = content.IndexOf('"');
        var lastQuote = content.LastIndexOf('"');

        if (firstQuote < 0 || lastQuote <= firstQuote)
        {
            return null;
        }

        var parts = content[(firstQuote + 1)..lastQuote].Split('~');
        if (parts.Length < 35)
        {
            return null;
        }

        var price = ReadDecimal(parts[3]);
        if (price <= 0)
        {
            return null;
        }

        return new StockQuote(
            Code: parts[2],
            Price: price,
            High: ReadDecimal(parts[33]),
            Low: ReadDecimal(parts[34]),
            Open: ReadDecimal(parts[5]),
            PreClose: ReadDecimal(parts[4]),
            Change: ReadDecimal(parts[31]),
            ChangePercent: ReadDecimal(parts[32]));
    }

    private static decimal ReadDecimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }
}

internal static class EastMoneyQuoteClient
{
    public static async Task<StockQuote?> GetQuoteAsync(HttpClient httpClient, string code)
    {
        var stockCode = StockCode.Normalize(code);
        var fields = "f43,f44,f45,f46,f57,f60,f169,f170";
        var url = $"https://push2.eastmoney.com/api/qt/stock/get?secid={stockCode.SecId}&fields={fields}&ut=fa5fd1943c7b386f172d6893dbfba10b";
        using var stream = await httpClient.GetStreamAsync(url);
        using var document = await JsonDocument.ParseAsync(stream);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var price = ReadScaledDecimal(data, "f43");
        if (price <= 0)
        {
            return null;
        }

        return new StockQuote(
            Code: data.GetProperty("f57").GetString() ?? stockCode.PureCode,
            Price: price,
            High: ReadScaledDecimal(data, "f44"),
            Low: ReadScaledDecimal(data, "f45"),
            Open: ReadScaledDecimal(data, "f46"),
            PreClose: ReadScaledDecimal(data, "f60"),
            Change: ReadScaledDecimal(data, "f169"),
            ChangePercent: ReadScaledDecimal(data, "f170"));
    }

    private static decimal ReadScaledDecimal(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number / 100m,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var number) => number / 100m,
            _ => 0
        };
    }
}

internal sealed record StockCode(string PureCode, string SecId)
{
    public static bool IsValid(string input)
    {
        var value = input.Trim().ToLowerInvariant();
        if (value.StartsWith("sh", StringComparison.Ordinal) || value.StartsWith("sz", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return value.Length == 6 && value.All(char.IsDigit);
    }

    public static StockCode Normalize(string input)
    {
        var value = input.Trim().ToLowerInvariant();
        var pureCode = value;

        if (value.StartsWith("sh", StringComparison.Ordinal) || value.StartsWith("sz", StringComparison.Ordinal))
        {
            pureCode = value[2..];
        }

        if (!IsValid(input))
        {
            throw new ArgumentException("股票代码格式不正确。");
        }

        var market = value.StartsWith("sh", StringComparison.Ordinal) || pureCode.StartsWith('6') ? "1" : "0";
        return new StockCode(pureCode, $"{market}.{pureCode}");
    }
}

internal sealed record StockQuote(
    string Code,
    decimal Price,
    decimal High,
    decimal Low,
    decimal Open,
    decimal PreClose,
    decimal Change,
    decimal ChangePercent);
