using System.Windows;
using System.Windows.Threading;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.App.Services;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.App.Views;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Infrastructure.Logging;
using WindowsSetupAssistant.Infrastructure.Persistence;
using WindowsSetupAssistant.Infrastructure.Winget;

namespace WindowsSetupAssistant.App;

/// <summary>
/// Điểm khởi động và cũng là "composition root" - nơi duy nhất lắp ráp các thành phần.
///
/// Dự án cố tình KHÔNG dùng thư viện DI ngoài: số lượng dịch vụ ít, việc khởi tạo tường minh
/// ở đây giúp người mới đọc hiểu ngay lớp nào phụ thuộc lớp nào.
/// </summary>
public partial class App : WpfApplication
{
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Bắt mọi lỗi chưa xử lý để ứng dụng không "tắt ngang" khi đang cài phần mềm.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            System.Diagnostics.Debug.WriteLine(args.ExceptionObject);

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();

        var themeManager = new ThemeManager();
        themeManager.Apply(settings.Theme);

        // --- Infrastructure ---
        var logger = new AppLogger();
        var processRunner = new ProcessRunner();
        var wingetService = new WingetService(processRunner, logger);
        var repository = new JsonProfileRepository(dataFilePath: null, logger: logger);

        // --- Application ---
        var queueService = new InstallationQueueService(wingetService, logger);

        // --- Presentation ---
        var dialogService = new DialogService();
        var logViewModel = new LogViewModel(logger, dialogService, logger.LogFilePath);

        _mainViewModel = new MainViewModel(
            repository,
            wingetService,
            queueService,
            logger,
            dialogService,
            themeManager,
            settingsStore,
            settings,
            logViewModel);

        AsyncRelayCommand.UnhandledExceptionHandler = exception =>
        {
            logger.Error($"Lỗi không xử lý được: {exception.Message}", details: exception.ToString());
            dialogService.ShowError("Đã xảy ra lỗi", exception.Message);
        };

        var window = new MainWindow { DataContext = _mainViewModel };
        var isClosing = false;
        window.Closing += async (_, args) =>
        {
            if (isClosing)
            {
                return;
            }

            args.Cancel = true;
            if (_mainViewModel is not null && await _mainViewModel.PrepareForCloseAsync())
            {
                isClosing = true;
                window.Close();
            }
        };

        MainWindow = window;
        window.Show();

        logger.Information("Ứng dụng Windows Setup Assistant khởi động.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Bảo đảm danh sách đã được ghi xuống đĩa trước khi thoát.
        try
        {
            _mainViewModel?.PrepareForCloseAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Đang thoát rồi thì không hiển thị lỗi nữa.
        }
        finally
        {
            _mainViewModel?.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ứng dụng gặp lỗi không mong đợi:\n\n{e.Exception.Message}",
            "Lỗi",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
