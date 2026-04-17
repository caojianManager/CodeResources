using System.Windows;
using System.Windows.Controls;

namespace BLETool;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void GattTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is BleViewModel viewModel)
        {
            viewModel.SelectGattNode(e.NewValue);
        }
    }
}
