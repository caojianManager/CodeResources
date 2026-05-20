using Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EEGTool.ViewModels
{
    public class StartupViewModel : ViewModelBase
    {
        public static StartupViewModel ShowWindow()
        {
            var viewModel = new StartupViewModel();
            _ = WindowManager.GetInstance().ShowWindowAsync(viewModel);
            return viewModel;
        }

        public void CloseWindow()
        {
            WindowManager.GetInstance().CloseWindow(this);
        }

    }
}
