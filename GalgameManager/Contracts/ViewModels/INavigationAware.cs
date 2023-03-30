namespace GalgameManager.Contracts.ViewModels;

public interface INavigationAware
{
    /// <summary>
    /// navigate to this page
    /// </summary>
    /// <param name="parameter"></param>
    void OnNavigatedTo(object parameter);

    /// <summary>
    /// navigate away from this page
    /// </summary>
    void OnNavigatedFrom();
}
