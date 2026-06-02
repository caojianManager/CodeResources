using EEGTool.Models.Template;
using Framework.MVVM.Commands;
using FrameWork.Common;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EEGTool.ViewModels.Template
{
    public class TemplateViewModel : BindableBase
    {
        public Action<List<string>>? UpdateElectrodeAction;
        private bool _isShowCreateTemplateWindow = false;
        private bool _isTemplateNameError;
        private string _templateNameErrorMessage = string.Empty;
        private bool _isCollectionDurationError;
        private string _collectionDurationErrorMessage = string.Empty;

        public bool IsShowCreateTemplateWindow
        {
            get => _isShowCreateTemplateWindow;
            set => SetProperty(ref _isShowCreateTemplateWindow, value);
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

        private readonly ObservableCollection<string> _comboxItems;
        public ReadOnlyObservableCollection<string> ComboxItems { get; }

        private TemplateInfoModel _template = new TemplateInfoModel();

        public TemplateInfoModel Template
        {
            get => _template;
            set
            {
                TemplateInfoModel newTemplate = value ?? new TemplateInfoModel();
                if (ReferenceEquals(_template, newTemplate))
                {
                    return;
                }

                if (_template != null)
                {
                    _template.PropertyChanged -= Template_PropertyChanged;
                }

                if (SetProperty(ref _template, newTemplate))
                {
                    newTemplate.PropertyChanged += Template_PropertyChanged;
                }
            }
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
        public ICommand? DeleteElectrodeCommand { get; set; }

        public TemplateViewModel()
        {
            Template.PropertyChanged += Template_PropertyChanged;
            _comboxItems = new ObservableCollection<string>(Constants.ChannelList.ToList());
            ComboxItems = new ReadOnlyObservableCollection<string>(_comboxItems);
            Config();
        }

        private void Template_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Template.Name) && !string.IsNullOrWhiteSpace(Template.Name))
            {
                IsTemplateNameError = false;
                TemplateNameErrorMessage = string.Empty;
            }

            if (e.PropertyName == nameof(Template.Time) && Template.Time > 0)
            {
                IsCollectionDurationError = false;
                CollectionDurationErrorMessage = string.Empty;
            }
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

            DeleteElectrodeCommand = new RelayCommand((o) => {
                DeleteElectrode(o);
            });

            CancelCreateCommand = new RelayCommand((o) =>
            {
                IsShowCreateTemplateWindow = false;
            });
        }

        private void SureCreateTemplate()
        {
            if (string.IsNullOrWhiteSpace(Template.Name))
            {
                IsTemplateNameError = true;
                TemplateNameErrorMessage = "模板名称不能为空";
                return;
            }

            if (Template.Time <= 0)
            {
                IsCollectionDurationError = true;
                CollectionDurationErrorMessage = "采集时长应大于 00:00:00";
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
            var newEle = new Electrode
            {
                Name = eleName,
                Channel = "Ch1",
            };
            newEle.UpdateChannelAction += () =>
            {
                Template.IsUpdateTemplate = true;
            };
            Template.EleDirectory.Add(newEle);
            Template.IsUpdateTemplate = true;
        }

        private void DeleteElectrode(object obj)
        {
            var ele = obj as Electrode;
            if (ele == null)
                return;

            var item = Template.EleDirectory
                .FirstOrDefault(e => e.Name.Equals(ele.Name));

            if (item != null)
            {
                Template.EleDirectory.Remove(item);
                var directoryItem = Template.EleDirectory
                    .FirstOrDefault(e => e.Name.Equals(ele.Name));
                if (directoryItem != null)
                {
                    Template.EleDirectory.Remove(directoryItem);
                }
                Template.IsUpdateTemplate = true;
            }
            var eleList = Template.EleDirectory.Select(e => e.Name).ToList();
            UpdateElectrodeAction?.Invoke(eleList);
            //刷新电极
        }

        private void CreateTemplate()
        {
            Template = new TemplateInfoModel
            {
                Time = 120
            };
            IsTemplateNameError = false;
            TemplateNameErrorMessage = string.Empty;
            IsCollectionDurationError = false;
            CollectionDurationErrorMessage = string.Empty;
            IsShowCreateTemplateWindow = true;
        }

        


    }
}
