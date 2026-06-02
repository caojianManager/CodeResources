using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FrameWork.UserControls.NumericUpDown
{
    public partial class BZNumericUpDown : UserControl
    {
        public BZNumericUpDown()
        {
            InitializeComponent();
            Loaded += (s, e) => ValidateValue();
        }

        #region 依赖属性

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(BZNumericUpDown),
                new PropertyMetadata(Brushes.Black, OnForegroundChanged));

        public new Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(BZNumericUpDown),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set
            {
                double newVal = Math.Round(value, DecimalPlaces);
                if (newVal < MinValue) newVal = MinValue;
                if (newVal > MaxValue) newVal = MaxValue;
                SetValue(ValueProperty, newVal);
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (BZNumericUpDown)d;
            ctrl.TextBoxValue.Text = ctrl.GetDisplayText();
        }

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(BZNumericUpDown), new PropertyMetadata(0.0));

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(BZNumericUpDown), new PropertyMetadata(100.0));

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(double), typeof(BZNumericUpDown), new PropertyMetadata(1.0));

        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public static readonly DependencyProperty IsDecimalProperty =
            DependencyProperty.Register(nameof(IsDecimal), typeof(bool), typeof(BZNumericUpDown), new PropertyMetadata(false));

        public bool IsDecimal
        {
            get => (bool)GetValue(IsDecimalProperty);
            set => SetValue(IsDecimalProperty, value);
        }

        public static readonly DependencyProperty DecimalPlacesProperty =
            DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(BZNumericUpDown), new PropertyMetadata(1));

        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        public static readonly DependencyProperty InputBorderBrushProperty =
            DependencyProperty.Register(nameof(InputBorderBrush), typeof(Brush), typeof(BZNumericUpDown), new PropertyMetadata(Brushes.LightGray));

        public Brush InputBorderBrush
        {
            get => (Brush)GetValue(InputBorderBrushProperty);
            set => SetValue(InputBorderBrushProperty, value);
        }

        public static readonly DependencyProperty InputBorderThicknessProperty =
            DependencyProperty.Register(nameof(InputBorderThickness), typeof(Thickness), typeof(BZNumericUpDown), new PropertyMetadata(new Thickness(1)));

        public Thickness InputBorderThickness
        {
            get => (Thickness)GetValue(InputBorderThicknessProperty);
            set => SetValue(InputBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty IsDurationProperty =
            DependencyProperty.Register(nameof(IsDuration), typeof(bool), typeof(BZNumericUpDown), new PropertyMetadata(false, OnDisplayModeChanged));

        public bool IsDuration
        {
            get => (bool)GetValue(IsDurationProperty);
            set => SetValue(IsDurationProperty, value);
        }

        private static void OnDisplayModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (BZNumericUpDown)d;
            if (ctrl.TextBoxValue != null)
            {
                ctrl.TextBoxValue.Text = ctrl.GetDisplayText();
            }
        }

        #endregion

        #region 事件逻辑

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (BZNumericUpDown)d;
            if (ctrl.TextBoxValue != null)
                ctrl.TextBoxValue.Foreground = (Brush)e.NewValue;
        }

        private void Plus_Click(object sender, RoutedEventArgs e)
        {
            Value += Step;
            ValidateValue();
        }

        private void Minus_Click(object sender, RoutedEventArgs e)
        {
            Value -= Step;
            ValidateValue();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsInputValidAfterInsert(e.Text);
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                ValidateValue();
                e.Handled = true;
            }
        }

        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var pasteText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!IsInputValidAfterInsert(pasteText))
            {
                e.CancelCommand();
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateValue();
        }

        #endregion

        #region 辅助逻辑

        private void ValidateValue()
        {
            if (TryParseTextValue(TextBoxValue.Text, out double val))
            {
                if (val < MinValue) val = MinValue;
                if (val > MaxValue) val = MaxValue;
                Value = Math.Round(val, DecimalPlaces);
            }
            else
            {
                Value = MinValue;
            }

            TextBoxValue.Text = GetDisplayText();
        }

        private bool IsInputValidAfterInsert(string input)
        {
            var proposedText = BuildProposedText(input);
            return IsTextAllowed(proposedText);
        }

        private string BuildProposedText(string input)
        {
            var currentText = TextBoxValue.Text ?? string.Empty;
            var selectionStart = TextBoxValue.SelectionStart;
            var selectionLength = TextBoxValue.SelectionLength;
            return currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, input);
        }

        private bool IsTextAllowed(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (IsDuration)
                return IsDurationStructureValid(text);

            if (!IsDecimal || DecimalPlaces <= 0)
                return Regex.IsMatch(text, @"^\d+$");

            var decimalSeparator = Regex.Escape(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            return Regex.IsMatch(text, $@"^\d*({decimalSeparator}\d{{0,{DecimalPlaces}}})?$");
        }

        private bool TryParseTextValue(string text, out double value)
        {
            if (IsDuration)
            {
                if (TryParseDuration(text, out var totalSeconds))
                {
                    value = totalSeconds;
                    return true;
                }

                value = 0;
                return false;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private string GetDisplayText()
        {
            if (IsDuration)
            {
                return FormatDuration((int)Math.Round(Value));
            }

            return Value.ToString($"F{DecimalPlaces}");
        }

        private static bool IsDurationStructureValid(string text)
        {
            if (text.Length > 8)
            {
                return false;
            }

            for (var index = 0; index < text.Length; index++)
            {
                var currentChar = text[index];
                if (index == 2 || index == 5)
                {
                    if (currentChar != ':')
                    {
                        return false;
                    }

                    continue;
                }

                if (!char.IsDigit(currentChar))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseDuration(string text, out int totalSeconds)
        {
            totalSeconds = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var hours) ||
                !int.TryParse(parts[1], out var minutes) ||
                !int.TryParse(parts[2], out var seconds) ||
                hours < 0 || hours > 99 ||
                minutes < 0 || minutes > 59 ||
                seconds < 0 || seconds > 59)
            {
                return false;
            }

            totalSeconds = hours * 3600 + minutes * 60 + seconds;
            return true;
        }

        private static string FormatDuration(int totalSeconds)
        {
            totalSeconds = Math.Max(0, totalSeconds);
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        #endregion
    }
}
