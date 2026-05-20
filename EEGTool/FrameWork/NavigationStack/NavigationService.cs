using FrameWork.MVVM;
using System.Windows;

namespace FrameWork.NavigationStack
{
    public class NavigationService : INavigationService
    {
        // 栈中同时存 ViewModel 和对应 View
        private readonly Stack<(BaseViewModel vm, FrameworkElement view)> _stack = new();

        private NavigationHost? _host;
        private static readonly Lazy<NavigationService> _instance = new(() => new NavigationService());
        public static NavigationService Instance => _instance.Value;

        public bool CanGoBack => _stack.Count > 1;

        private bool _isNavigating = false;

        internal void RegisterHost(NavigationHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public async Task InitializeAsync<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel
        {
            _stack.Clear();
            var vm = CreateViewModel<TViewModel>();
            var view = CreateViewForViewModel(vm);
            _stack.Push((vm, view));

            if (vm is INavigationAware aware)
                await aware.OnNavigatedToAsync(parameter);

            await _host!.ShowContentAsyn(view, animateForward: true);
        }

        public async Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel
        {
            if (_isNavigating) return;

            _isNavigating = true;
            try
            {
                var vm = CreateViewModel<TViewModel>();
                var view = CreateViewForViewModel(vm);

                _stack.Push((vm, view));

                if (vm is INavigationAware aware)
                    await aware.OnNavigatedToAsync(parameter);

                await _host!.ShowContentAsyn(view, animateForward: true);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        public async Task NavigateBackAsync(object? result = null)
        {
            if (!CanGoBack || _isNavigating)
                return;

            _isNavigating = true;
            try
            {
                var current = _stack.Pop();
                if (current.vm is INavigationAware oldAware)
                    await oldAware.OnNavigatedFromAsync();

                var previous = _stack.Peek();

                await _host!.ShowContentAsyn(previous.view, animateForward: false);

                if (previous.vm is INavigationAware prevAware)
                    await prevAware.OnNavigatedToAsync(result);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private static BaseViewModel CreateViewModel<TViewModel>() where TViewModel : BaseViewModel
        {
            return Activator.CreateInstance<TViewModel>();
        }

        private static FrameworkElement CreateViewForViewModel(BaseViewModel vm)
        {
            var viewTypeName = vm.GetType().FullName!.Replace("ViewModel", "View");
            var viewType = Type.GetType(viewTypeName) ?? throw new InvalidOperationException($"View not found for {vm.GetType().Name}");
            if (Activator.CreateInstance(viewType) is not FrameworkElement view)
                throw new InvalidOperationException($"Cannot create {viewType.Name}");

            view.DataContext = vm;
            return view;
        }
    }
}
