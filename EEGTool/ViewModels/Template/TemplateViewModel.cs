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
        private string _templateName = string.Empty;
        private bool _isTemplateNameError;
        private string _templateNameErrorMessage = string.Empty;

        public bool IsShowCreateTemplateWindow
        {
            get => _isShowCreateTemplateWindow;
            set => SetProperty(ref _isShowCreateTemplateWindow, value);
        }

        public string TemplateName
        {
            get => _templateName;
            set
            {
                if (SetProperty(ref _templateName, value) && !string.IsNullOrWhiteSpace(value))
                {
                    IsTemplateNameError = false;
                    TemplateNameErrorMessage = string.Empty;
                }
            }
        }

        public bool IsTemplateNameError
        {
            get => _isTemplateNameError;
            set => SetProperty(ref _isTemplateNameError, value);
        }

        public string TemplateNameErrorMessage
        {
            get => _templateNameErrorMessage;
            set => SetProperty(ref _templateNameErrorMessage, value);
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
            if (string.IsNullOrWhiteSpace(TemplateName))
            {
                IsTemplateNameError = true;
                TemplateNameErrorMessage = "模板名称不能为空";
                return;
            }

            IsTemplateNameError = false;
            TemplateNameErrorMessage = string.Empty;
            IsShowCreateTemplateWindow = false;
        }


        private void CreateTemplate()
        {
            TemplateName = string.Empty;
            IsTemplateNameError = false;
            TemplateNameErrorMessage = string.Empty;
            IsShowCreateTemplateWindow = true;
        }


    }
}
