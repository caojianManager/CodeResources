using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace FrameWork.Helper
{
    public static class WatermarkService
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(WatermarkService),
                new PropertyMetadata(null, OnPlaceholderChanged));

        public static void SetPlaceholder(DependencyObject element, string value) => element.SetValue(PlaceholderProperty, value);
        public static string GetPlaceholder(DependencyObject element) => (string)element.GetValue(PlaceholderProperty);

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb)
            {
                tb.Loaded -= Tb_Loaded;
                tb.Loaded += Tb_Loaded;

                tb.TextChanged -= Tb_TextChanged;
                tb.TextChanged += Tb_TextChanged;

                tb.GotKeyboardFocus -= Tb_GotKeyboardFocus;
                tb.GotKeyboardFocus += Tb_GotKeyboardFocus;

                tb.LostKeyboardFocus -= Tb_LostKeyboardFocus;
                tb.LostKeyboardFocus += Tb_LostKeyboardFocus;

                // initial
                if (tb.IsLoaded) UpdateAdorner(tb);
            }
        }

        private static void Tb_LostKeyboardFocus(object sender, RoutedEventArgs e) => UpdateAdorner(sender as TextBox);
        private static void Tb_GotKeyboardFocus(object sender, RoutedEventArgs e) => UpdateAdorner(sender as TextBox);
        private static void Tb_TextChanged(object sender, TextChangedEventArgs e) => UpdateAdorner(sender as TextBox);
        private static void Tb_Loaded(object sender, RoutedEventArgs e) => UpdateAdorner(sender as TextBox);

        private static void UpdateAdorner(TextBox tb)
        {
            if (tb == null) return;
            var layer = AdornerLayer.GetAdornerLayer(tb);
            if (layer == null) return;

            // remove old watermark adorners
            var adorners = layer.GetAdorners(tb);
            if (adorners != null)
            {
                foreach (var a in adorners)
                {
                    if (a is WatermarkAdorner) layer.Remove(a);
                }
            }

            // show watermark only when text is empty and not focused (可根据需要调整)
            if (string.IsNullOrEmpty(tb.Text) && !tb.IsKeyboardFocused)
            {
                var placeholder = GetPlaceholder(tb);
                if (!string.IsNullOrEmpty(placeholder))
                {
                    layer.Add(new WatermarkAdorner(tb, placeholder));
                }
            }
        }

        // 内部 Adorner 类
        private class WatermarkAdorner : Adorner
        {
            private readonly TextBlock _textBlock;
            public WatermarkAdorner(UIElement adornedElement, string watermarkText) : base(adornedElement)
            {
                IsHitTestVisible = false;
                _textBlock = new TextBlock
                {
                    Text = watermarkText,
                    Foreground = Brushes.Gray,
                    Opacity = 0.7,
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                AddVisualChild(_textBlock);
            }

            protected override int VisualChildrenCount => 1;
            protected override Visual GetVisualChild(int index) => _textBlock;

            protected override Size MeasureOverride(Size constraint)
            {
                _textBlock.Measure(constraint);
                return constraint;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                _textBlock.Arrange(new Rect(finalSize));
                return finalSize;
            }
        }
    }
}


