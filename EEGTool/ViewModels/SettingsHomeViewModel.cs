using EEGTool.Views.Basics;
using Framework.Event;
using Framework.MVVM.Commands;
using FrameWork.Common;
using FrameWork.Event;
using FrameWork.MVVM;
using System;
using System.Windows;
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
        public ICommand? SaveCommand { get; set; }

        private string _bleTargetServiceUuid = string.Empty;
        public string BleTargetServiceUuid
        {
            get => _bleTargetServiceUuid;
            set => SetProperty(ref _bleTargetServiceUuid, value);
        }

        private string _bleTargetServiceUuidInput = string.Empty;
        public string BleTargetServiceUuidInput
        {
            get => _bleTargetServiceUuidInput;
            set => SetProperty(ref _bleTargetServiceUuidInput, value);
        }

        private double _impedanceTargetValue;
        public double ImpedanceTargetValue
        {
            get => _impedanceTargetValue;
            set => SetProperty(ref _impedanceTargetValue, value);
        }

        private double _impedanceGainNum;
        public double ImpedanceGainNum
        {
            get => _impedanceGainNum;
            set => SetProperty(ref _impedanceGainNum, value);
        }

        private double _impedanceLeafOff;
        public double ImpedanceLeafOff
        {
            get => _impedanceLeafOff;
            set => SetProperty(ref _impedanceLeafOff, value);
        }

        private double _seriesResistorKohm;
        public double SeriesResistorKohm
        {
            get => _seriesResistorKohm;
            set => SetProperty(ref _seriesResistorKohm, value);
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

            SaveCommand = new RelayCommand((o) =>
            {
                SaveSettings();
            });
        }

        private void LoadImpedancePreferences()
        {
            var config = Config.Instance;
            BleTargetServiceUuid = config.BleTargetServiceUuid;
            BleTargetServiceUuidInput = config.BleTargetServiceUuid;
            ImpedanceTargetValue = config.Impedance_TargetFreq;
            ImpedanceGainNum = config.ImpedanceGain;
            ImpedanceLeafOff = config.Lead_Of;
            SeriesResistorKohm = config.series_resistor_kohm;
        }

        private void SaveSettings()
        {
            if (!Guid.TryParse(BleTargetServiceUuidInput, out _))
            {
                MessageBox.Show("BLE目标特征格式不正确，请输入有效的UUID。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var config = Config.Instance;
                config.BleTargetServiceUuid = BleTargetServiceUuidInput;
                config.Impedance_TargetFreq = ImpedanceTargetValue;
                config.ImpedanceGain = ImpedanceGainNum;
                config.Lead_Of = ImpedanceLeafOff;
                config.series_resistor_kohm = SeriesResistorKohm;
                config.Save();
                BleTargetServiceUuid = config.BleTargetServiceUuid;

                MessageBox.Show("配置保存成功。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"配置保存失败：{ex.Message}", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
            LoadImpedancePreferences();
        }
    }
}
