using EEGTool.ViewModels;
using FrameWork.Common;
using log4net;
using log4net.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EEGTool
{ //APP 统一入口
    public class AppBootstrapper : Singleton<AppBootstrapper>
    {
        public void OnStartUp()
        {
            SystemInit();
            _ = InitView();
        }
        private void SystemInit()
        {
            Config.Instance.Init();
            SystemConfig.GetInstance().LoadConfig();


            //log4net 日志开关
#if DEBUG
            ILoggerRepository repository = LogManager.GetRepository();
            repository.Threshold = log4net.Core.Level.All;
#else
            ILoggerRepository repository = LogManager.GetRepository();
            repository.Threshold = log4net.Core.Level.Off;
#endif
        }
        private async Task InitView()
        {
            var starupVM = StartupViewModel.ShowWindow();
            await Task.Delay(2000);
            //HomePageViewModel.ShowWindow();
            //starupVM.CloseWindow();
        }
    }

}
