using EEGTool.Views.Common;
using System.Windows.Controls;
using EEGTool.ViewModels.Template;
using System.Windows;

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
            DataContextChanged += TemplateView_DataContextChanged;
            Config();
        }

        private void TemplateView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Config();
        }

        private void Config()
        {
            if (_viewModel != null)
            {
                _viewModel.UpdateElectrodeAction = null;
            }

            _viewModel = this.DataContext as TemplateViewModel;
            if (_viewModel == null)
            {
                return;
            }

            _viewModel.UpdateElectrodeAction = UpdateElectrode;
            BrainAreaView.SelectElectrode = SelectElectrode;
            _viewModel.UpdateElectrodeSelection();
        }

        private void SelectElectrode(string eleName)
        {
            _viewModel?.AddElectrode(eleName);
        }

        private void UpdateElectrode(List<string> electrodes)
        {
            TemplateBrainAreaView.UpdateElectrode(electrodes);
            BrainAreaView.UpdateElectrode(electrodes);
        }
    }
}
