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
using EEGTool.ViewModels.DeviceConnect;

namespace EEGTool.Views.DeviceConnect
{
    /// <summary>
    /// BLEConnectView.xaml 的交互逻辑
    /// </summary>
    public partial class BLEConnectView : UserControl
    {
        public BLEConnectView()
        {
            InitializeComponent();
            Loaded += BLEConnectView_Loaded;
            Unloaded += BLEConnectView_Unloaded;
        }

        private void BLEConnectView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DeviceConnectViewModel vm && vm.ScanDeviceCommand?.CanExecute(null) == true)
            {
                vm.ScanDeviceCommand.Execute(null);
            }
        }

        private async void BLEConnectView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DeviceConnectViewModel vm)
            {
                await vm.OnViewUnloadedAsync();
            }
        }
    }
}
