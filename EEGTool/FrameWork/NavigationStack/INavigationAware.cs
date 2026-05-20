
namespace FrameWork.NavigationStack
{
    public interface INavigationAware
    {
        Task OnNavigatedToAsync(object parameter);
        Task OnNavigatedFromAsync();
    }
}
