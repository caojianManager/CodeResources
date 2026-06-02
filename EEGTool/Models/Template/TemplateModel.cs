using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EEGTool.Models.Template
{
    public class TemplateModel : BindableBase
    {
        private string _templateId = string.Empty;
        public string TemplateId
        {
            get => _templateId;
            set => SetProperty(ref _templateId, value);
        }

        private int _time = 0;
        public int Time
        {
            get => _time;
            set => SetProperty(ref _time, value);
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private ObservableCollection<Electrode> _eleDirectory = new ObservableCollection<Electrode>();
        public ObservableCollection<Electrode> EleDirectory
        {
            get => _eleDirectory;
            set => SetProperty(ref _eleDirectory, value);
        }

    }

    public class TemplateInfoModel : TemplateModel
    {
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsUpdateTemplate { get; set; } = false;

    }

    public class Electrode : BindableBase
    {
        public Action? UpdateChannelAction;
        public string Name { get; set; } = string.Empty;
        private string _channel = string.Empty;
        public string Channel
        {
            get => _channel;
            set
            {
                if (value != _channel)
                    UpdateChannelAction?.Invoke();
                SetProperty(ref _channel, value);
            }
        }
    }
}
