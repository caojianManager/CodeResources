using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Framework
{
    public interface IWindowManager
    {
        void ShowWindow(object viewModel);
        void CloseWindow(object viewModel);
        void ActivateWindow(object viewModel);
        void ShowDialog(object viewModel);
        Task ShowWindowAsync(object viewModel);
    }

}
