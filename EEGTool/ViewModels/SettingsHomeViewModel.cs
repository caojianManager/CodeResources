using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Common;
using FrameWork.Event;
using FrameWork.MVVM;
using System;
using System.Windows.Input;

namespace EEGTool.ViewModels
{
    public class SettingsHomeViewModel : BindableBase, IApplicationContentView
    {
        public string Name => "系统设置";
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

        private bool _isLoadingPreferences;

        private double _impedanceTargetValue;
        public double ImpedanceTargetValue
        {
            get => _impedanceTargetValue;
            set
            {
                if (SetProperty(ref _impedanceTargetValue, value))
                {
                    SaveImpedancePreferences();
                }
            }
        }

        private double _impedanceGainNum;
        public double ImpedanceGainNum
        {
            get => _impedanceGainNum;
            set
            {
                if (SetProperty(ref _impedanceGainNum, value))
                {
                    SaveImpedancePreferences();
                }
            }
        }

        private double _impedanceLeafOff;
        public double ImpedanceLeafOff
        {
            get => _impedanceLeafOff;
            set
            {
                if (SetProperty(ref _impedanceLeafOff, value))
                {
                    SaveImpedancePreferences();
                }
            }
        }

        public SettingsHomeViewModel()
        {
            ConfigureCommands();
            LoadImpedancePreferences();
        }

        public void Init()
        {
            LoadImpedancePreferences();
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

        private void LoadImpedancePreferences()
        {
            _isLoadingPreferences = true;

            var config = Config.Instance;
            ImpedanceTargetValue = config.Impedance_TargetFreq;
            ImpedanceGainNum = config.ImpedanceGain;
            ImpedanceLeafOff = config.Lead_Of;

            _isLoadingPreferences = false;
        }

        private void SaveImpedancePreferences()
        {
            if (_isLoadingPreferences)
            {
                return;
            }

            var config = Config.Instance;
            config.Impedance_TargetFreq = ImpedanceTargetValue;
            config.ImpedanceGain = ImpedanceGainNum;
            config.Lead_Of = ImpedanceLeafOff;
            config.Save();
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
