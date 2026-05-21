using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Event;
using FrameWork.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EEGTool.ViewModels
{
    public class CollectionHomeViewModel : BindableBase, IApplicationContentView
    {
        public string Name => "采集主页";
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isInit = false;
        public bool IsInit
        {
            get => _isInit;
            set => SetProperty(ref _isInit, value);
        }

        public ICommand? BackHomeCommand { get; set; }

        private bool _isDeviceConnectTabSelected = true;
        public bool IsDeviceConnectTabSelected
        {
            get => _isDeviceConnectTabSelected;
            set
            {
                if (SetProperty(ref _isDeviceConnectTabSelected, value))
                {
                    OnPropertyChanged(nameof(IsCollectionConfigTabSelected));
                }
            }
        }

        public bool IsCollectionConfigTabSelected
        {
            get => !IsDeviceConnectTabSelected;
            set => IsDeviceConnectTabSelected = !value;
        }

        public void Init()
        {
            Config();
        }

        private void Config()
        {
            BackHomeCommand = new RelayCommand((o) =>
            {
                EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(MainViewModel));
            });
        }

        private void ClickPlaybackBtn()
        {

        }

        private void ClickCollectionBtn()
        {

        }

        public void OnHide()
        {

        }

        public void OnShow()
        {

        }
    }
}
