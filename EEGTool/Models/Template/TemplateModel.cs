using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
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
            set
            {
                if (SetProperty(ref _time, value))
                {
                    OnPropertyChanged(nameof(FormattedTime));
                }
            }
        }

        [JsonIgnore]
        public string FormattedTime
        {
            get
            {
                var totalSeconds = Math.Max(0, Time);
                var hours = totalSeconds / 3600;
                var minutes = totalSeconds % 3600 / 60;
                var seconds = totalSeconds % 60;
                return $"{hours:00}:{minutes:00}:{seconds:00}";
            }
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

        private bool _isChannelConflict;

        [JsonIgnore]
        public bool IsChannelConflict
        {
            get => _isChannelConflict;
            set => SetProperty(ref _isChannelConflict, value);
        }

        private string _channel = string.Empty;
        public string Channel
        {
            get => _channel;
            set
            {
                if (SetProperty(ref _channel, value))
                {
                    UpdateChannelAction?.Invoke();
                }
            }
        }
    }
}
