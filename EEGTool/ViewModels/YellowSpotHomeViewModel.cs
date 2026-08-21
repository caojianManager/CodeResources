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
    public class YellowSpotHomeViewModel : BindableBase,IApplicationContentView
    {

        public string Name => "黄斑变性";
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

        public YellowSpotHomeViewModel()
        {
            ConfigureCommands();
        }

        public void Init()
        {

        }

        private void ConfigureCommands()
        {
            if (BackHomeCommand != null)
            {
                return;
            }

            BackHomeCommand = new RelayCommand((o) =>
            {
                EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(MainViewModel));
            });
        }

        public void OnHide()
        {

        }

        public void OnShow()
        {

        }
    }
}
