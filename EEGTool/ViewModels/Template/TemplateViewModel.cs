using EEGTool.Models.Template;
using Framework.MVVM.Commands;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EEGTool.ViewModels.Template
{
    public class TemplateViewModel : BindableBase
    {
        private static readonly Regex DurationRegex = new Regex(@"^\d{2}:[0-5]\d:[0-5]\d$");
        private bool _isShowCreateTemplateWindow = false;
        private string _templateName = string.Empty;
        private bool _isTemplateNameError;
        private string _templateNameErrorMessage = string.Empty;
        private string _collectionDuration = string.Empty;
        private double _collectionDurationSeconds;
        private bool _isCollectionDurationError;
        private string _collectionDurationErrorMessage = string.Empty;

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

        public string CollectionDuration
        {
            get => _collectionDuration;
            set
            {
                if (SetProperty(ref _collectionDuration, value) && !string.IsNullOrWhiteSpace(value))
                {
                    CollectionDurationSeconds = ParseDurationSeconds(value);
                    IsCollectionDurationError = false;
                    CollectionDurationErrorMessage = string.Empty;
                }
            }
        }

        public double CollectionDurationSeconds
        {
            get => _collectionDurationSeconds;
            set
            {
                var roundedValue = Math.Round(value);
                if (SetProperty(ref _collectionDurationSeconds, roundedValue))
                {
                    _collectionDuration = FormatDuration((int)roundedValue);
                    OnPropertyChanged(nameof(CollectionDuration));
                    if (roundedValue > 0)
                    {
                        IsCollectionDurationError = false;
                        CollectionDurationErrorMessage = string.Empty;
                    }
                }
            }
        }

        private TemplateInfoModel _template = null;

        public TemplateInfoModel Template
        {
            get => _template;
            set => SetProperty(ref _template, value);
        }

        public bool IsCollectionDurationError
        {
            get => _isCollectionDurationError;
            set => SetProperty(ref _isCollectionDurationError, value);
        }

        public string CollectionDurationErrorMessage
        {
            get => _collectionDurationErrorMessage;
            set => SetProperty(ref _collectionDurationErrorMessage, value);
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

            if (string.IsNullOrWhiteSpace(CollectionDuration))
            {
                IsCollectionDurationError = true;
                CollectionDurationErrorMessage = "采集时长不能为空";
                return;
            }

            if (!IsDurationValid(CollectionDuration))
            {
                IsCollectionDurationError = true;
                CollectionDurationErrorMessage = "采集时长应大于 00:00:00，格式为 HH:mm:ss";
                return;
            }

            IsTemplateNameError = false;
            TemplateNameErrorMessage = string.Empty;
            IsCollectionDurationError = false;
            CollectionDurationErrorMessage = string.Empty;
            IsShowCreateTemplateWindow = false;
        }

        public void AddElectrode(string eleName)
        {
            if (Template == null)
                Template = new TemplateInfoModel();
            var newEle = new Electrode
            {
                Name = eleName,
                Channel = "Ch1",
            };
            newEle.UpdateChannelAction += () =>
            {
                Template.IsUpdateTemplate = true;
            };
            Template.Electrodes.Add(newEle);
            Template.EleDirectory.Add(newEle);
            Template.IsUpdateTemplate = true;
        }


        private static bool IsDurationValid(string duration)
        {
            if (!DurationRegex.IsMatch(duration))
            {
                return false;
            }

            var parts = duration.Split(':');
            if (!int.TryParse(parts[0], out var hours) ||
                !int.TryParse(parts[1], out var minutes) ||
                !int.TryParse(parts[2], out var seconds) ||
                hours > 99)
            {
                return false;
            }

            return hours * 3600 + minutes * 60 + seconds > 0;
        }


        private void CreateTemplate()
        {
            TemplateName = string.Empty;
            IsTemplateNameError = false;
            TemplateNameErrorMessage = string.Empty;
            CollectionDuration = "00:02:00";
            IsCollectionDurationError = false;
            CollectionDurationErrorMessage = string.Empty;
            IsShowCreateTemplateWindow = true;
        }

        private static double ParseDurationSeconds(string duration)
        {
            var parts = duration.Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var hours) ||
                !int.TryParse(parts[1], out var minutes) ||
                !int.TryParse(parts[2], out var seconds))
            {
                return 0;
            }

            return hours * 3600 + minutes * 60 + seconds;
        }

        private static string FormatDuration(int totalSeconds)
        {
            totalSeconds = Math.Max(0, totalSeconds);
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        


    }
}
