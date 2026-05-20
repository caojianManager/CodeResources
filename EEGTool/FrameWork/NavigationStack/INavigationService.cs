using FrameWork.MVVM;

namespace FrameWork.NavigationStack
{
    public interface INavigationService
    {
        Task InitializeAsync<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel;
        Task NavigateToAsync<TViewModel>(object? parameter = null) where TViewModel : BaseViewModel;
        Task NavigateBackAsync(object? result = null);
        bool CanGoBack { get; }
    }
}
