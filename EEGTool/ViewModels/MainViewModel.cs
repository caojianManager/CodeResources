using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using EEGTool.Views.Basics;
using FrameWork.MVVM;
using Framework.MVVM.Commands;

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
