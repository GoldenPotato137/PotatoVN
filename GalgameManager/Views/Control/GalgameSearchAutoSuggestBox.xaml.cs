using System.Collections.ObjectModel;
using System.Windows.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control
{
    public sealed partial class GalgameSearchAutoSuggestBox: UserControl
    {
        public GalgameSearchAutoSuggestBox()
        {
            InitializeComponent();
            _galgameCollectionService = (GalgameCollectionService)App.GetService<IGalgameCollectionService>();
        }

        private GalgameCollectionService _galgameCollectionService;
        
        private const int SearchDelay = 500;
    
        public readonly ObservableCollection<string> SearchSuggestions = new();
        private DateTime _lastSearchTime = DateTime.Now;

        public static readonly DependencyProperty SearchKeyProperty = DependencyProperty.Register(
            nameof(SearchKey),
            typeof(string),
            typeof(GalgameSearchAutoSuggestBox),
            new PropertyMetadata(string.Empty));

        public string SearchKey
        {
            get => (string)GetValue(SearchKeyProperty);
            set => SetValue(SearchKeyProperty, value);
        }
        
        public ICommand? SearchCommand
        {
            get => (ICommand)GetValue(SearchCommandProperty);
            set => SetValue(SearchCommandProperty, value);
        }
    
        public static readonly DependencyProperty SearchCommandProperty = DependencyProperty.Register(nameof(SearchCommand),
            typeof(ICommand), typeof(GalgameSearchAutoSuggestBox), new PropertyMetadata(null));

        private async void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(SearchKey))
            {
                SearchCommand?.Execute(SearchKey);
                SearchSuggestions.Clear();
                return;
            }
        
            _ = Task.Run((async Task() =>
            {
                _lastSearchTime = DateTime.Now;
                DateTime tmp = _lastSearchTime;
                await Task.Delay(SearchDelay);
                if (tmp == _lastSearchTime) //如果在延迟时间内没有再次输入，则开始搜索
                {
                    await UiThreadInvokeHelper.InvokeAsync(() =>
                    {
                        SearchCommand?.Execute(SearchKey);
                    });
                }
            })!);
            //更新建议
            if(args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            if (SearchKey == string.Empty)
            {
                SearchSuggestions.Clear();
                return;
            }
            List<string> result = await _galgameCollectionService.GetSearchSuggestions(SearchKey);
            SearchSuggestions.Clear();
            foreach (var suggestion in result)
                SearchSuggestions.Add(suggestion);
        }

        private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrEmpty(SearchKey)) return;
            SearchCommand?.Execute(SearchKey);
        }
    }
}