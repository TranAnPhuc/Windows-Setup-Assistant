using WindowsSetupAssistant.Domain.Localization;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using WindowsSetupAssistant.App.Cli;
using WindowsSetupAssistant.App.Localization;
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

    /// <summary>Khác null nghĩa là đang chạy chế độ không giám sát: tuyệt đối không hiện hộp thoại.</summary>
    private IUnattendedOutput? _unattendedOutput;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Bắt mọi lỗi chưa xử lý TRƯỚC khi phân nhánh dòng lệnh: nhánh không giám sát cũng
        // đang cài phần mềm và cũng cần lưới an toàn này, không chỉ riêng giao diện. Nếu hai
        // dòng đăng ký này nằm sau điểm return của nhánh không giám sát thì nhánh đó chạy mà
        // không có bất kỳ trình bắt lỗi toàn cục nào - một ngoại lệ trong callback tiến trình
        // con hay trong Progress<T> (chạy ngoài ngăn xếp try/catch của RunCommandLineAsync) sẽ
        // làm sập cứng tiến trình, không in lỗi, không trả về mã thoát nào trong hợp đồng 0..4.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            System.Diagnostics.Debug.WriteLine(args.ExceptionObject);

        var parseResult = CommandLineParser.Parse(e.Args);

        if (parseResult.Mode != CommandLineMode.Gui)
        {
            RunCommandLine(parseResult);
            return;
        }

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();

        var language = LanguageCatalog.Resolve(settings.Language, CultureInfo.InstalledUICulture);
        LocalizationSource.Instance.SetLanguage(language);
        settings.Language = language.Name;
        settingsStore.Save(settings);

        var themeManager = new ThemeManager();
        themeManager.Apply(settings.Theme);

        // --- Infrastructure ---
        var localizer = LocalizationSource.Instance.Localizer;
        var logger = new AppLogger(logDirectory: null, writeToFile: true, localizer);
        var processRunner = new ProcessRunner();
        var wingetService = new WingetService(processRunner, logger);
        var repository = new JsonProfileRepository(dataFilePath: null, logger: logger, localizer: localizer);

        // --- Application ---
        var queueService = new InstallationQueueService(wingetService, logger);
        var scanService = new MachineScanService(wingetService, logger);
        var backupExporter = new BackupExporter();

        // --- Presentation ---
        var dialogService = new DialogService();
        var logViewModel = new LogViewModel(logger, dialogService, logger.LogFilePath, localizer);

        _mainViewModel = new MainViewModel(
            repository,
            wingetService,
            queueService,
            scanService,
            backupExporter,
            logger,
            dialogService,
            themeManager,
            settingsStore,
            settings,
            logViewModel,
            localizer);

        AsyncRelayCommand.UnhandledExceptionHandler = exception =>
        {
            logger.Error($"Lỗi không xử lý được: {exception.Message}", details: exception.ToString());
            dialogService.ShowError("Đã xảy ra lỗi", exception.Message);
        };

        var window = new MainWindow { DataContext = _mainViewModel };
        MainWindow = window;
        window.Show();

        logger.Information(LocalizedText.Of(MessageKeys.AppStarted));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // KHÔNG gọi PrepareForCloseAsync().GetAwaiter().GetResult() ở đây.
        //
        // OnExit chạy trên chính UI thread. Chặn UI thread để chờ một tác vụ async
        // mà tác vụ đó lại cần quay về UI thread để chạy tiếp (ConfigureAwait(true))
        // sẽ gây DEADLOCK: cửa sổ đã đóng nhưng tiến trình không bao giờ thoát.
        // Đây là lỗi đã tái hiện được: đóng cửa sổ xong, process vẫn sống mãi.
        //
        // Việc dừng hàng đợi và lưu dữ liệu đã được MainWindow.OnClosing thực hiện
        // đầy đủ (và await đúng cách) TRƯỚC khi cửa sổ được phép đóng.
        // Ngoài ra danh sách còn được tự lưu ngay sau mỗi thay đổi, nên đến đây
        // chỉ cần giải phóng tài nguyên.
        _mainViewModel?.Dispose();

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Ở chế độ không giám sát KHÔNG được hiện hộp thoại: một hộp thoại đứng chờ người bấm
        // sẽ treo máy đang chạy không người trông cho tới khi có ai đó đi ngang qua.
        if (_unattendedOutput is { } output)
        {
            output.WriteError(ConsoleMessages.UnexpectedError(e.Exception.Message));
            e.Handled = true;
            Shutdown((int)UnattendedExitCode.SomePackagesFailed);
            return;
        }

        MessageBox.Show(
            $"Ứng dụng gặp lỗi không mong đợi:\n\n{e.Exception.Message}",
            "Lỗi",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    /// <summary>
    /// Nhánh dòng lệnh. KHÔNG tạo cửa sổ nào.
    ///
    /// OnStartup là hàm đồng bộ nên không await được ở đây: ta khởi chạy tác vụ rồi trả về,
    /// vòng lặp thông điệp của WPF sẽ bơm tiếp các đoạn await sau đó. Điều đó chỉ đúng khi
    /// ShutdownMode là OnExplicitShutdown - mặc định OnLastWindowClose sẽ đóng ứng dụng ngay
    /// vì không có cửa sổ nào cả.
    /// </summary>
    private void RunCommandLine(CommandLineParseResult parseResult)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _ = RunCommandLineAsync(parseResult);
    }

    private async Task RunCommandLineAsync(CommandLineParseResult parseResult)
    {
        var exitCode = UnattendedExitCode.InvalidArguments;
        using var cancellation = new CancellationTokenSource();

        ConsoleCancelEventHandler? cancelHandler = null;

        // ConsoleSession.Attach() phải nằm TRONG try: ShutdownMode đã là OnExplicitShutdown
        // và nhánh này không tạo cửa sổ nào, nên nếu Attach() ném ngoại lệ mà nằm ngoài try thì
        // không finally nào chạy, Shutdown(...) không bao giờ được gọi, và tiến trình treo mãi
        // mãi trên một máy không có ai trông.
        ConsoleSession? console = null;

        try
        {
            console = ConsoleSession.Attach();
            _unattendedOutput = console;

            switch (parseResult.Mode)
            {
                case CommandLineMode.Help:
                    console.WriteLine(ConsoleMessages.BuildHelp());
                    exitCode = UnattendedExitCode.Success;
                    break;

                case CommandLineMode.Invalid:
                    console.WriteError(parseResult.ErrorMessage ?? ConsoleMessages.InvalidArguments);
                    console.WriteLine();
                    console.WriteLine(ConsoleMessages.BuildHelp());
                    exitCode = UnattendedExitCode.InvalidArguments;
                    break;

                default:
                    cancelHandler = (_, args) =>
                    {
                        // Cancel = true để Windows không giết tiến trình ngay: ta cần kịp
                        // dừng gói đang cài và ghi báo cáo.
                        args.Cancel = true;
                        console.WriteLine(ConsoleMessages.CancelRequested);
                        cancellation.Cancel();
                    };

                    Console.CancelKeyPress += cancelHandler;

                    exitCode = await RunUnattendedAsync(parseResult.Options!, console, cancellation.Token)
                        .ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception exception)
        {
            console?.WriteError(ConsoleMessages.UnexpectedError(exception.Message));
            exitCode = UnattendedExitCode.SomePackagesFailed;
        }
        finally
        {
            if (cancelHandler is not null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }

            console?.Dispose();
            _unattendedOutput = null;

            Shutdown((int)exitCode);
        }
    }

    /// <summary>
    /// Lắp ráp đúng các thành phần mà giao diện đang dùng, chỉ khác: không ViewModel, không cửa sổ.
    /// </summary>
    private static async Task<UnattendedExitCode> RunUnattendedAsync(
        CommandLineOptions options,
        IUnattendedOutput output,
        CancellationToken cancellationToken)
    {
        var localizer = new ResourceStringLocalizer();
        var logger = new AppLogger(logDirectory: null, writeToFile: true, localizer);
        var processRunner = new ProcessRunner();
        var wingetService = new WingetService(processRunner, logger);
        var repository = new JsonProfileRepository(dataFilePath: null, logger: logger, localizer: localizer);
        var queueService = new InstallationQueueService(wingetService, logger);

        var runner = new UnattendedRunner(
            wingetService, repository, queueService, localizer, output, logger);

        return await runner.RunAsync(options, cancellationToken).ConfigureAwait(true);
    }
}
