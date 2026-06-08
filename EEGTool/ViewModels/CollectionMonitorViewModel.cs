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
using System.Windows;
using System.Windows.Input;
using EEGTool.Models.Collection;
using EEGTool.Models.Template;
using CommandManager = EEGTool.Models.BLE.CommandManager;
using Logger = FrameWork.Log.Logger;

namespace EEGTool.ViewModels
{

    public class CollectionMonitorViewModel : BindableBase,IApplicationContentView
    {

        public string Name => "采集监测页面";
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
        public ICommand? StartRecordCommand { get; set; }

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
                var result = MessageBox.Show(
                    $"确定要结束采集吗？",
                    "结束采集",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.OK)
                {
                    return;
                }

                EventUtilManager.EventUitl.OnEvent<Type>(EventName.SWITCH_PAGE_WITH_TYPE, typeof(MainViewModel));
            });
        }

        /// <summary>
        /// 开始监测
        /// </summary>
        private void StartMonitor()
        {
            //1.获取采集配置信息
            var cInfo = CollectionInfoManager.GetInstance().Info;

            //2.配置采集指令-并发送给下位机(MCU);
            var channelList = TemplateFileManager.GetInstance()
                .GetCurrentChannelList(cInfo.Template)
                .Where(channel => channel >= 1 && channel <= CommandManager.ChannelCount)
                .Distinct()
                .ToList();

            if (channelList.Count == 0)
            {
                Logger.Info("[CollectionMonitorViewModel][StartMonitor]:当前模板没有有效通道，默认开启16通道采集");
                channelList = Enumerable.Range(1, CommandManager.ChannelCount).ToList();
            }
            ushort channelMask = CommandManager.BuildChannelMask(channelList);
            ushort sampleRate = cInfo.SampleRate > 0 ? (ushort)cInfo.SampleRate : (ushort)250;
            ushort durationSeconds = cInfo.Template.Time > 0 ? (ushort)cInfo.Template.Time : (ushort)60;

            byte[] command = CommandManager.BuildConfigureCollectionCommand(
                channelMask,
                sampleRate,
                durationSeconds);
            Logger.Info($"[CollectionMonitorViewModel][StartMonitor]:采集配置指令 {CommandManager.ToHexString(command)}");

        }


        /// <summary>
        /// 停止监测
        /// </summary>
        private void StopMonitor()
        {

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
            StartMonitor();
        }

    }
}
