using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views.Control;

public sealed partial class ObservableList
{
    public DataTemplate ItemTemplate
    {
        get => (DataTemplate)GetValue(ItemTemplateProperty);
        set
        {
            SetValue(ItemTemplateProperty, value);
            ItemsRepeater.ItemTemplate = new ObservableListTemplateSelector(this);
        }
    }

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(nameof(ItemTemplate),
        typeof(DataTemplate), typeof(ObservableList), new PropertyMetadata(null));
    
    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource),
        typeof(IEnumerable), typeof(ObservableList), new PropertyMetadata(null, OnItemsSourceChanged));
    
    public ICommand? AddCommand
    {
        get => (ICommand)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }
    
    public static readonly DependencyProperty AddCommandProperty = DependencyProperty.Register(nameof(AddCommand),
        typeof(ICommand), typeof(ObservableList), new PropertyMetadata(null));
    
    // ReSharper disable once CollectionNeverQueried.Global
    public ObservableCollection<object> InternalItems = new();
    
    public ObservableList()
    {
        InitializeComponent();
        ItemsRepeater.ItemTemplate = new ObservableListTemplateSelector(this);
    }
    
    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ObservableList control) return;
        if ((e.OldValue as IEnumerable)! is INotifyCollectionChanged oldNotifyCollection)
            oldNotifyCollection.CollectionChanged -= control.OnExternalCollectionChanged;
        if ((e.NewValue as IEnumerable)! is INotifyCollectionChanged newNotifyCollection)
            newNotifyCollection.CollectionChanged += control.OnExternalCollectionChanged;
        control.SyncItems();
    }

    private void SyncItems()
    {
        InternalItems.Clear();
        foreach (var item in ItemsSource)
            InternalItems.Add(item);
        InternalItems.Add(new AddButtonPlaceholder()); //添加按钮
    }

    private void OnExternalCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncItems();
    }
    
    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        AddCommand?.Execute(null);
    }
    
    private class AddButtonPlaceholder{}
    
    private class ObservableListTemplateSelector : DataTemplateSelector
    {
        private readonly DataTemplate _template;
        private readonly DataTemplate _addButtonTemplate;
        
        public ObservableListTemplateSelector(ObservableList list)
        {
            _template = list.ItemTemplate;
            _addButtonTemplate = list.AddButtomTemplate;
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item is AddButtonPlaceholder ? _addButtonTemplate : _template;
        }
    }
}
