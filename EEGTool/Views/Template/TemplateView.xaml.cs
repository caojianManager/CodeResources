using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

namespace EEGTool.Views.Template
{
    /// <summary>
    /// TemplateView.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateView : UserControl
    {
        private static readonly Regex DigitsOnlyRegex = new Regex("^[0-9]+$");

        public TemplateView()
        {
            InitializeComponent();
        }

        private void DurationTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnlyRegex.IsMatch(e.Text);
        }

        private void DurationTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(typeof(string)))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(typeof(string)) as string;
            if (string.IsNullOrEmpty(text) || !DigitsOnlyRegex.IsMatch(text))
            {
                e.CancelCommand();
            }
        }
    }
}
