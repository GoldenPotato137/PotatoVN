using GalgameManager.ViewModels;

namespace GalgameManager.Views
{
    public sealed partial class PlayedTimePage
    {
        public PlayedTimeViewModel ViewModel
        {
            get;
        }

        public PlayedTimePage()
        {
            ViewModel = App.GetService<PlayedTimeViewModel>();
            InitializeComponent();
            DataContext = ViewModel;
        }
    }
}