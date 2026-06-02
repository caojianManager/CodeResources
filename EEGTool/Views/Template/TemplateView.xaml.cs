using EEGTool.Views.Common;
using System.Windows.Controls;
using EEGTool.ViewModels.Template;

namespace EEGTool.Views.Template
{
    /// <summary>
    /// TemplateView.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateView : UserControl
    {
        private TemplateViewModel? _viewModel;

        public TemplateView()
        {
            InitializeComponent();
            Config();
        }

        private void Config()
        {
            _viewModel = this.DataContext as TemplateViewModel;
            if (_viewModel == null)
            {
                return;
            }

            _viewModel.UpdateElectrodeAction = UpdateElectrode;
            BrainAreaView.SelectElectrode += (eleName) =>
            {
                _viewModel.AddElectrode(eleName);
            };
        }

        private void UpdateElectrode(List<string> electrodes)
        {
            BrainAreaView.UpdateElectrode(electrodes);
        }
    }
}
