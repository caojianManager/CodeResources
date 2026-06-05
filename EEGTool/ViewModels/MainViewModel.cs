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
    public class MainViewModel : BindableBase,IApplicationContentView
    {
        public string Name => "首页";
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

        public ICommand? CollectionCommand { get; set; }

        public ICommand? PlaybackCommand { get; set; }
        public ICommand? TemplateCommand { get; set; }


        public void Init()
        {
            Config();
        }

        private void Config()
        {
            PlaybackCommand = new RelayCommand((o) =>
            {
                ClickPlaybackBtn();
            });

            CollectionCommand = new RelayCommand((o) =>
            {
                ClickCollectionBtn();
            });

            TemplateCommand = new RelayCommand((o) =>
            {
                ClickTemplateBtn();
            });
        }

        private void ClickTemplateBtn()
        {
            EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(TemplateHomeViewModel));
        }

        private void ClickPlaybackBtn()
        {

        }

        private void ClickCollectionBtn()
        {
            //EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(CollectionHomeViewModel));

            //Todo:cajian-临时测试地方后面恢复
            EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(CollectionMonitorViewModel));
        }

        public void OnHide()
        {

        }

        public void OnShow()
        {

        }
    }
}
