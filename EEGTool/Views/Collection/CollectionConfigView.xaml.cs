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
using EEGTool.ViewModels.Collection;

namespace EEGTool.Views.Collection
{
    /// <summary>
    /// CollectionConfigView.xaml 的交互逻辑
    /// </summary>
    public partial class CollectionConfigView : UserControl
    {
        public CollectionConfigView()
        {
            InitializeComponent();
            Loaded += CollectionConfigView_Loaded;
            Unloaded += CollectionConfigView_Unloaded;
        }

        private void CollectionConfigView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CollectionConfigViewModel vm)
            {
                vm.OnViewLoaded();
            }
        }

        private void CollectionConfigView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CollectionConfigViewModel vm)
            {
                vm.OnViewUnloaded();
            }
        }
    }
}
