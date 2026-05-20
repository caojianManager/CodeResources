using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EEGTool.Views.Basics;
using FrameWork.MVVM;

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
        public void Init()
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
