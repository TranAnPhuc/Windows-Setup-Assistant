using System.Collections.Specialized;
using System.Windows;
using WindowsSetupAssistant.App.ViewModels;

namespace WindowsSetupAssistant.App.Views;

/// <summary>
/// Code-behind chỉ làm những việc thuần giao diện mà XAML không làm được:
/// gọi lệnh khởi động và tự cuộn bảng nhật ký. Mọi logic nghiệp vụ nằm ở ViewModel.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        _viewModel = viewModel;

        // Tự cuộn xuống dòng nhật ký mới nhất.
        ((INotifyCollectionChanged)viewModel.Logs.Entries).CollectionChanged += OnLogEntriesChanged;

        viewModel.LoadedCommand.Execute(null);
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.Logs.AutoScroll || LogGrid.Items.Count == 0)
        {
            return;
        }

        LogGrid.ScrollIntoView(LogGrid.Items[^1]);
    }
}
