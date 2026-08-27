using FrameWork.MVVM;
using Framework;
using FrameWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EEGTool.ViewModels.Filters
{
    public class FilterConfigViewModel : BindableBase, IWindowClose
    {
        private static FilterConfigViewModel? _instance;

        public FilterConfigViewModel() {
            
        }

        public static FilterConfigViewModel Show()
        {
            if (_instance != null)
            {
                WindowManager.GetInstance().ActivateWindow(_instance);
                return _instance;
            }

            _instance = new FilterConfigViewModel();
            WindowManager.GetInstance().ShowWindow(_instance);
            return _instance;
        }

        public static FilterConfigViewModel ShowWindow()
        {
            return Show();
        }

        public void Close()
        {
            WindowManager.GetInstance().CloseWindow(this);
        }

        public void CloseWindow()
        {
            Close();
        }

        public void OnWindowClose()
        {
            _instance = null;
        }
    }
}
