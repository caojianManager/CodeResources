using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using FrameWork.MVVM;
using Framework.MVVM.Commands;

namespace EEGTool.ViewModels.Template
{
    public class TemplateViewModel : BindableBase
    {
        private bool _isShowCreateTemplateWindow = false;

        public bool IsShowCreateTemplateWindow
        {
            get => _isShowCreateTemplateWindow;
            set => SetProperty(ref _isShowCreateTemplateWindow, value);
        }

        public ICommand? CreateTemplateCommand { get; set; }
        public ICommand? CancelCreateCommand { get; set; }
        public ICommand? SureCreateTemplateCommand { get; set; }

        public TemplateViewModel()
        {
            Config();
        }

        private void Config()
        {
            CreateTemplateCommand = new RelayCommand((o) =>
            {
                CreateTemplate();
            });

            SureCreateTemplateCommand = new RelayCommand((o) =>
            {
                SureCreateTemplate();
            });

            CancelCreateCommand = new RelayCommand((o) =>
            {
                IsShowCreateTemplateWindow = false;
            });
        }

        private void SureCreateTemplate()
        {
            IsShowCreateTemplateWindow = false;
        }


        private void CreateTemplate()
        {
            IsShowCreateTemplateWindow = true;
        }


    }
}
