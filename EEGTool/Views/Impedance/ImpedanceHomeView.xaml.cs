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
using EEGTool.ViewModels.Impedance;

namespace EEGTool.Views.Impedance
{
    /// <summary>
    /// ImpedanceHomeView.xaml 的交互逻辑
    /// </summary>
    public partial class ImpedanceHomeView : UserControl
    {
        public static readonly DependencyProperty BackCommandProperty = DependencyProperty.Register(
            nameof(BackCommand),
            typeof(ICommand),
            typeof(ImpedanceHomeView),
            new PropertyMetadata(null));

        public ICommand? BackCommand
        {
            get => (ICommand?)GetValue(BackCommandProperty);
            set => SetValue(BackCommandProperty, value);
        }

        public ImpedanceHomeView()
        {
            InitializeComponent();
            IsVisibleChanged += OnIsVisibleChanged;
            Unloaded += OnUnloaded;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is not ImpedanceHomeViewModel viewModel)
            {
                return;
            }

            if (IsVisible)
            {
                viewModel.OnShow();
            }
            else
            {
                viewModel.OnHide();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ImpedanceHomeViewModel viewModel)
            {
                viewModel.OnHide();
            }
        }
    }
}
