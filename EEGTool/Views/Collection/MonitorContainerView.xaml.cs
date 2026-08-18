using EEGTool.ViewModels.Collection;
using System.Windows.Controls;
using System.Windows.Input;

namespace EEGTool.Views.Collection
{
    /// <summary>
    /// MonitorContainerView.xaml 的交互逻辑
    /// </summary>
    public partial class MonitorContainerView : UserControl
    {
        public MonitorContainerView()
        {
            InitializeComponent();
        }

        private void LayoutIconButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (DataContext is MonitorContainerViewModel viewModel &&
                viewModel.ShowLayoutFlyoutCommand.CanExecute(null))
            {
                viewModel.ShowLayoutFlyoutCommand.Execute(null);
            }
        }
    }
}
