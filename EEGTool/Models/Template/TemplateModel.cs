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
        public string TemplateId { get; set; } = string.Empty;
        public int Time { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public List<Electrode> EleDirectory { get; set; } = new();

    }

    public class TemplateInfoModel : TemplateModel
    {
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private string _showName = string.Empty;
        public string ShowName
        {
            get => _showName;
            set => SetProperty(ref _showName, value);
        }

        private string _showTime = string.Empty;
        public string ShowTime
        {
            get => _showTime;
            set => SetProperty(ref _showTime, value);
        }

        private ObservableCollection<Electrode> _electrodes = new ObservableCollection<Electrode>();
        public ObservableCollection<Electrode> Electrodes
        {
            get => _electrodes;
            set => SetProperty(ref _electrodes, value);
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
