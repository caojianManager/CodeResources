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
            ctrl.TextBoxValue.Text = ctrl.Value.ToString($"F{ctrl.DecimalPlaces}");
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
            if (double.TryParse(TextBoxValue.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double val))
            {
                if (val < MinValue) val = MinValue;
                if (val > MaxValue) val = MaxValue;
                Value = Math.Round(val, DecimalPlaces);
            }
            else
            {
                Value = MinValue;
            }

            TextBoxValue.Text = Value.ToString($"F{DecimalPlaces}");
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

            if (!IsDecimal || DecimalPlaces <= 0)
                return Regex.IsMatch(text, @"^\d+$");

            var decimalSeparator = Regex.Escape(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            return Regex.IsMatch(text, $@"^\d*({decimalSeparator}\d{{0,{DecimalPlaces}}})?$");
        }

        #endregion
    }
}
