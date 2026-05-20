using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Framework;
using FrameWork;

namespace EEGTool.ViewModels
{
    public class HomePageViewModel : ViewModelBase, IWindowShow
    {
        public void OnWindowShow()
        {
            
        }

        public static void ShowWindow()
        {
            var viewModel = new HomePageViewModel();
            _ = WindowManager.GetInstance().ShowWindowAsync(viewModel);
        }

    }
}
