using EEGTool.Views.Basics;
using Framework;
using FrameWork;
using FrameWork.Event;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EEGTool.ViewModels
{
    public class HomePageViewModel : ViewModelBase, IWindowShow
    {

        private readonly ObservableCollection<IApplicationContentView>? _pages;
        public ReadOnlyObservableCollection<IApplicationContentView> Pages { get; }

        private IApplicationContentView? _selectedPage;

        public IApplicationContentView? SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (value == null)
                {
                    value = Pages.FirstOrDefault();
                }

                //修改上一个页面的状态
                if (_selectedPage != null)
                {
                    _selectedPage.OnHide();
                    _selectedPage.IsSelected = false;
                }

                if (value != null && !value.IsInit)
                {
                    Task.Run(() =>
                    {
                        value.Init();
                    }).ContinueWith((task) => value.IsInit = true);
                }

                SetProperty(ref _selectedPage, value);
                value?.OnShow();
                value.IsSelected = true;
            }
        }

        public HomePageViewModel()
        {
            _pages = new ObservableCollection<IApplicationContentView>(CreateAllPages());
            Pages = new ReadOnlyObservableCollection<IApplicationContentView>(_pages);
            SelectedPage = Pages.FirstOrDefault();
            Config();
        }

        private void Config()
        {
            EventUtilManager.EventUitl.AddEvent<Type>(Framework.Event.EventName.SWITCH_PAGE_WITH_TYPE,
                (type) => { SwithViewPortPage(type); });
        }


        private IEnumerable<IApplicationContentView> CreateAllPages()
        {
            yield return new MainViewModel();
            yield return new CollectionHomeViewModel();
            yield return new TemplateHomeViewModel();
        }

        private void SwithViewPortPage(Type type)
        {
            SelectedPage = Pages.FirstOrDefault(p => p.GetType() == type);
        }


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
