using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using WindowsSetupAssistant.App.ViewModels;

namespace WindowsSetupAssistant.App.Views;

/// <summary>
/// Code-behind chỉ làm những việc thuần giao diện mà XAML không làm được:
/// gọi lệnh khởi động, tự cuộn bảng nhật ký và xử lý thao tác đóng cửa sổ.
/// Mọi logic nghiệp vụ nằm ở ViewModel.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    /// <summary>Đã hỏi xong và được phép đóng - lần Closing kế tiếp không hỏi lại.</summary>
    private bool _allowClose;

    /// <summary>Đang chờ ViewModel dừng hàng đợi và lưu dữ liệu.</summary>
    private bool _closeRequestPending;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
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

    /// <summary>
    /// Chặn việc đóng cửa sổ cho tới khi ViewModel xác nhận là an toàn.
    ///
    /// Vì sao phải huỷ thao tác đóng rồi mới gọi Close() lần nữa?
    /// Việc dừng hàng đợi và ghi file JSON là bất đồng bộ, trong khi sự kiện Closing lại
    /// đồng bộ - không thể "await" ngay trong đó. Cách xử lý chuẩn của WPF là:
    /// huỷ lần đóng này, chạy tác vụ bất đồng bộ, khi xong mới gọi Close() lại.
    ///
    /// PrepareForCloseAsync trả về false khi người dùng trả lời "No" ở hộp xác nhận
    /// lúc đang cài, hoặc khi chưa lưu được dữ liệu - cả hai trường hợp đều giữ cửa sổ lại.
    /// </summary>
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _viewModel is null)
        {
            return;
        }

        e.Cancel = true;

        // Người dùng bấm X nhiều lần trong lúc đang chờ - chỉ xử lý một lần.
        if (_closeRequestPending)
        {
            return;
        }

        _closeRequestPending = true;

        try
        {
            if (await _viewModel.PrepareForCloseAsync())
            {
                CloseForReal();
            }
        }
        catch (Exception ex)
        {
            // "async void" bắt buộc phải tự bắt lỗi, nếu không ứng dụng sẽ sập.
            var answer = MessageBox.Show(
                this,
                $"Không hoàn tất được việc dừng hàng đợi và lưu dữ liệu:\n\n{ex.Message}\n\nVẫn thoát?",
                "Lỗi khi đóng ứng dụng",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer == MessageBoxResult.Yes)
            {
                CloseForReal();
            }
        }
        finally
        {
            _closeRequestPending = false;
        }
    }

    /// <summary>
    /// Đóng cửa sổ thật sự.
    ///
    /// BẮT BUỘC phải đẩy qua Dispatcher chứ không gọi Close() trực tiếp.
    /// Lý do: khi ViewModel không có gì phải chờ (không cài đặt, không có gì để lưu),
    /// PrepareForCloseAsync hoàn tất ĐỒNG BỘ, nên "await" ở trên không nhả luồng và
    /// đoạn mã này vẫn đang nằm bên trong sự kiện Closing. Gọi Close() lúc đó sẽ bị WPF
    /// ném lỗi "Cannot call Close while a Window is closing" và cửa sổ KHÔNG đóng.
    /// Lỗi này phụ thuộc thời điểm nên lúc xảy ra lúc không - đã tái hiện được bằng
    /// kịch bản kiểm thử giao diện thật.
    ///
    /// BeginInvoke bảo đảm Close() chỉ chạy sau khi sự kiện Closing kết thúc.
    /// </summary>
    private void CloseForReal()
    {
        _allowClose = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            try
            {
                Close();
            }
            catch (InvalidOperationException)
            {
                // Ứng dụng đã bắt đầu tắt bằng đường khác (ví dụ khởi động lại bằng quyền Admin).
            }
        }));
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
