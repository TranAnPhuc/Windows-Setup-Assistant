using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsSetupAssistant.App.Converters;
using WindowsSetupAssistant.App.Services;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.App.Views;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Infrastructure.Logging;
using WindowsSetupAssistant.Infrastructure.Persistence;

namespace DocScreenshotCapture;

public class MockWingetService : IWingetService
{
    public Task<WingetAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WingetAvailability.Available("v1.29.290"));
    }

    public Task<IReadOnlyList<WingetPackageInfo>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WingetPackageInfo> results = new List<WingetPackageInfo>
        {
            new("Zalo", "VNG.Zalo", "24.5.1", null, "winget"),
            new("Telegram Desktop", "Telegram.TelegramDesktop", "5.1.7", null, "winget"),
            new("Foxit PDF Reader", "Foxit.FoxitReader", "2024.2.0", null, "winget"),
            new("Viber", "Viber.Viber", "22.8.0", null, "winget"),
            new("Skype", "Microsoft.Skype", "8.118.0", null, "winget")
        };
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<WingetPackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WingetPackageInfo> installed = new List<WingetPackageInfo>
        {
            new("Google Chrome", "Google.Chrome", "126.0.6478.127", null, "winget"),
            new("7-Zip 24.07 (x64)", "7zip.7zip", "24.07", null, "winget"),
            new("UniKey 4.3 RC5", "PhamKimLong.UniKey", "4.3.5", null, "winget"),
            new("VLC media player", "VideoLAN.VLC", "3.0.21", null, "winget"),
            new("Notepad++ (64-bit x64)", "Notepad++.Notepad++", "8.6.8", null, "winget")
        };
        return Task.FromResult(installed);
    }

    public Task<bool> IsInstalledAsync(string packageId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(packageId.Contains("Chrome") || packageId.Contains("7zip") || packageId.Contains("UniKey"));
    }

    public Task<InstallationResult> InstallAsync(SoftwarePackage package, IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InstallationResult
        {
            PackageId = package.PackageId,
            DisplayName = package.Name,
            Outcome = InstallOutcome.Succeeded,
            Duration = TimeSpan.FromSeconds(12),
            Message = "Cài đặt thành công."
        });
    }

    public Task<InstallationResult> UpgradeAsync(SoftwarePackage package, IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new InstallationResult
        {
            PackageId = package.PackageId,
            DisplayName = package.Name,
            Outcome = InstallOutcome.Upgraded,
            Duration = TimeSpan.FromSeconds(10),
            Message = "Nâng cấp thành công."
        });
    }
}

public class Program
{
    private static readonly string OutputDir = @"D:\Desktop\AI\Windows-Setup-Assistant\docs\images";

    [STAThread]
    public static void Main()
    {
        Console.WriteLine("Bắt đầu tạo bộ ảnh chụp màn hình chất lượng cao cho tài liệu hướng dẫn...");

        if (!Directory.Exists(OutputDir))
        {
            Directory.CreateDirectory(OutputDir);
        }

        var app = new System.Windows.Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 1. Nạp Themes và Converters
        var dictLight = new ResourceDictionary { Source = new Uri("pack://application:,,,/WindowsSetupAssistant;component/Themes/Light.xaml", UriKind.Absolute) };
        var dictControls = new ResourceDictionary { Source = new Uri("pack://application:,,,/WindowsSetupAssistant;component/Themes/Controls.xaml", UriKind.Absolute) };
        app.Resources.MergedDictionaries.Add(dictLight);
        app.Resources.MergedDictionaries.Add(dictControls);

        app.Resources.Add("ResourceKeyToBrush", new ResourceKeyToBrushConverter());
        app.Resources.Add("InverseBoolean", new InverseBooleanConverter());
        app.Resources.Add("BoolToVisibility", new BoolToVisibilityConverter());
        app.Resources.Add("InverseBoolToVisibility", new InverseBoolToVisibilityConverter());
        app.Resources.Add("StringToVisibility", new StringToVisibilityConverter());
        app.Resources.Add("LogLevelToBrush", new LogLevelToBrushConverter());
        app.Resources.Add("ExitCode", new ExitCodeConverter());
        app.Resources.Add("InstallOutcome", new InstallOutcomeConverter());
        app.Resources.Add("InstallOutcomeToBrush", new InstallOutcomeToBrushConverter());
        app.Resources.Add("Duration", new WindowsSetupAssistant.App.Converters.DurationConverter());

        // Khởi tạo các dịch vụ nền
        var logger = new AppLogger();
        var dataFile = @"D:\Desktop\AI\Windows-Setup-Assistant\publish-final\Data\software-list.json";
        var repository = new JsonProfileRepository(dataFilePath: dataFile, logger: logger);
        var wingetService = new MockWingetService();
        var queueService = new InstallationQueueService(wingetService, logger);
        var scanService = new MachineScanService(wingetService, logger);
        var backupExporter = new BackupExporter();
        var dialogService = new DialogService();
        var themeManager = new ThemeManager();
        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();
        settings.CheckInstalledOnStartup = false;
        var logViewModel = new LogViewModel(logger, dialogService, logger.LogFilePath);

        var mainVm = new MainViewModel(
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
            logViewModel);

        // Khởi tạo nạp dữ liệu
        mainVm.LoadedCommand.Execute(null);
        while (mainVm.LoadedCommand.IsRunning)
        {
            DoEvents();
            Thread.Sleep(50);
        }
        DoEvents();

        // Chọn Profile "Máy văn phòng"
        var officeProfile = mainVm.Profiles.FirstOrDefault(p => p.Name.Contains("văn phòng")) ?? mainVm.Profiles[0];
        mainVm.SelectedProfile = officeProfile;
        DoEvents();

        // -------------------------------------------------------------
        // ẢNH 1: Giao diện chính tổng quan (Tab Danh sách phần mềm)
        // -------------------------------------------------------------
        Console.WriteLine("Đang chụp 01_tong_quan_giao_dien.png...");
        foreach (var p in mainVm.Packages)
        {
            p.InstallState = InstallState.Unknown;
            p.IsSelected = true;
        }
        DoEvents();

        var win1 = new MainWindow { DataContext = mainVm, Width = 1200, Height = 760 };
        SaveElement(win1, "Windows Setup Assistant - Cài hàng loạt phần mềm bằng WinGet", Path.Combine(OutputDir, "01_tong_quan_giao_dien.png"), 1200, 760);

        // -------------------------------------------------------------
        // ẢNH 2: Giao diện sau khi bấm "Kiểm tra đã cài" (Đã phân loại trạng thái)
        // -------------------------------------------------------------
        Console.WriteLine("Đang chụp 02_kiem_tra_da_cai.png...");
        var installedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Google Chrome", "7-Zip", "Unikey", "Zalo", "VLC media player", "Notepad++"
        };

        foreach (var pkg in mainVm.Packages)
        {
            if (installedNames.Contains(pkg.Name))
            {
                pkg.InstallState = InstallState.Installed;
                pkg.InstalledVersion = "v126.0 (Mới nhất)";
                pkg.IsSelected = false; // Đã có trên máy -> bỏ tick
            }
            else
            {
                pkg.InstallState = InstallState.NotInstalled;
                pkg.IsSelected = true;  // Chưa có -> tick chọn
            }
        }
        DoEvents();

        var win2 = new MainWindow { DataContext = mainVm, Width = 1200, Height = 760 };
        SaveElement(win2, "Windows Setup Assistant - Kiểm tra trạng thái đã cài đặt", Path.Combine(OutputDir, "02_kiem_tra_da_cai.png"), 1200, 760);

        // -------------------------------------------------------------
        // ẢNH 3: Hộp thoại Xác nhận cài đặt (InstallConfirmWindow)
        // -------------------------------------------------------------
        Console.WriteLine("Đang chụp 03_hop_thoai_xac_nhan.png...");
        var selectedPkgs = mainVm.Packages.Where(p => p.IsSelected).ToList();
        var confirmVm = new InstallConfirmViewModel(selectedPkgs, ExistingPackageAction.Skip);
        var win3 = new InstallConfirmWindow { DataContext = confirmVm, Width = 640, Height = 580 };
        SaveElement(win3, "Xác nhận cài đặt - Windows Setup Assistant", Path.Combine(OutputDir, "03_hop_thoai_xac_nhan.png"), 640, 580);

        // -------------------------------------------------------------
        // ẢNH 4: Tab Tìm trên WinGet (Tra cứu và thêm phần mềm)
        // -------------------------------------------------------------
        Console.WriteLine("Đang chụp 04_tab_tim_kiem_winget.png...");
        mainVm.Search.SearchText = "zalo";
        mainVm.Search.SearchCommand.Execute(null);
        while (mainVm.Search.SearchCommand.IsRunning)
        {
            DoEvents();
            Thread.Sleep(50);
        }
        DoEvents();

        var win4 = new MainWindow { DataContext = mainVm, Width = 1200, Height = 760 };
        SaveElement(win4, "Windows Setup Assistant - Tìm kiếm gói trên WinGet", Path.Combine(OutputDir, "04_tab_tim_kiem_winget.png"), 1200, 760, w =>
        {
            var tabControl = FindVisualChild<TabControl>(w);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 1; // Tab 2: Tìm trên WinGet
            }
        });

        // -------------------------------------------------------------
        // ẢNH 5: Cửa sổ Thêm / Sửa phần mềm (PackageEditorWindow)
        // -------------------------------------------------------------
        Console.WriteLine("Đang chụp 05_them_sua_phan_mem.png...");
        var editorPkg = new SoftwarePackage
        {
            Name = "Foxit PDF Reader",
            PackageId = "Foxit.FoxitReader",
            Category = SoftwareCategory.Office,
            Notes = "Trình đọc tài liệu PDF văn phòng tiêu chuẩn"
        };
        var editorVm = new PackageEditorViewModel(editorPkg);
        var win5 = new PackageEditorWindow { DataContext = editorVm, Width = 560, Height = 530 };
        SaveElement(win5, "Sửa thông tin phần mềm", Path.Combine(OutputDir, "05_them_sua_phan_mem.png"), 560, 530);

        // -------------------------------------------------------------
        // ẢNH 6: Cửa sổ Quét & sao lưu máy này (ScanResultWindow)
        // -------------------------------------------------------------
        Console.WriteLine("Đang chụp 06_quet_sao_luu_may.png...");
        var scanEntries = new List<InstalledSoftwareEntry>
        {
            new("Google Chrome", "Google.Chrome", "126.0.6478.127", "winget", InstalledSoftwareKind.WingetPackage),
            new("7-Zip 24.07 (x64)", "7zip.7zip", "24.07", "winget", InstalledSoftwareKind.WingetPackage),
            new("Notepad++ (64-bit x64)", "Notepad++.Notepad++", "8.6.8", "winget", InstalledSoftwareKind.WingetPackage),
            new("UniKey 4.3 RC5", "PhamKimLong.UniKey", "4.3.5", "winget", InstalledSoftwareKind.WingetPackage),
            new("VLC media player", "VideoLAN.VLC", "3.0.21", "winget", InstalledSoftwareKind.WingetPackage),
            new("Visual Studio Code", "Microsoft.VisualStudioCode", "1.90.2", "winget", InstalledSoftwareKind.WingetPackage),
            new("Git", "Git.Git", "2.45.2", "winget", InstalledSoftwareKind.WingetPackage),
            new("Node.js", "OpenJS.NodeJS", "20.15.0", "winget", InstalledSoftwareKind.WingetPackage),
            new("WinRAR 7.01 (64-bit)", "RARLab.WinRAR", "7.01.0", "winget", InstalledSoftwareKind.WingetPackage),
            new("Zalo", "VNG.Zalo", "24.5.1", "winget", InstalledSoftwareKind.WingetPackage),
            new("Telegram Desktop", "Telegram.TelegramDesktop", "5.1.7", "winget", InstalledSoftwareKind.WingetPackage),
            // Các phần mềm cài tay (Manual Only)
            new("MISA SME.NET 2023", "{MISA-SME-2023-GUID}", "R25.0", null, InstalledSoftwareKind.ManualOnly),
            new("HTKK - Hỗ trợ kê khai thuế", "HTKK_TongCucThue", "5.1.9", null, InstalledSoftwareKind.ManualOnly),
            new("Phần mềm Chữ ký số Viettel-CA", "Viettel.TokenManager", "2.0.1", null, InstalledSoftwareKind.ManualOnly)
        };

        var snapshot = new MachineSnapshot
        {
            MachineName = "DESKTOP-VANPHONG-01",
            ScannedAt = DateTimeOffset.Now,
            Entries = scanEntries
        };
        var scanVm = new ScanResultViewModel(snapshot);
        var win6 = new ScanResultWindow { DataContext = scanVm, Width = 960, Height = 720 };
        SaveElement(win6, "Kết quả quét phần mềm - DESKTOP-VANPHONG-01", Path.Combine(OutputDir, "06_quet_sao_luu_may.png"), 960, 720);

        // -------------------------------------------------------------
        // ẢNH 7: Tab Nhật ký (Theo dõi tiến trình & tra cứu mã lỗi)
        // -------------------------------------------------------------
        Console.WriteLine("Đang chụp 07_tab_nhat_ky.png...");
        logViewModel.Entries.Clear();
        logViewModel.Entries.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now.AddSeconds(-95),
            Level = LogLevel.Information,
            Message = "Ứng dụng Windows Setup Assistant khởi động thành công."
        });
        logViewModel.Entries.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now.AddSeconds(-90),
            Level = LogLevel.Information,
            Message = "Phát hiện WinGet phiên bản v1.29.290 sẵn sàng."
        });
        logViewModel.Entries.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now.AddSeconds(-65),
            Level = LogLevel.Information,
            Message = "Bắt đầu cài đặt hàng loạt 3 phần mềm..."
        });
        logViewModel.Entries.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now.AddSeconds(-45),
            Level = LogLevel.Information,
            Message = "Cài đặt thành công: Google Chrome",
            Command = "winget install --id Google.Chrome --silent --accept-package-agreements",
            ExitCode = 0
        });
        logViewModel.Entries.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now.AddSeconds(-30),
            Level = LogLevel.Information,
            Message = "Cài đặt thành công: 7-Zip",
            Command = "winget install --id 7zip.7zip --silent --accept-package-agreements",
            ExitCode = 0
        });
        logViewModel.Entries.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now.AddSeconds(-15),
            Level = LogLevel.Information,
            Message = "Cài đặt thành công: Notepad++",
            Command = "winget install --id Notepad++.Notepad++ --silent --accept-package-agreements",
            ExitCode = 0
        });
        logViewModel.Entries.Add(new LogEntry
        {
            Timestamp = DateTimeOffset.Now.AddSeconds(-5),
            Level = LogLevel.Information,
            Message = "Hoàn tất toàn bộ hàng đợi cài đặt (3/3 thành công)."
        });

        var win7 = new MainWindow { DataContext = mainVm, Width = 1200, Height = 760 };
        SaveElement(win7, "Windows Setup Assistant - Nhật ký hệ thống", Path.Combine(OutputDir, "07_tab_nhat_ky.png"), 1200, 760, w =>
        {
            var tabControl = FindVisualChild<TabControl>(w);
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 3; // Tab 4: Nhật ký
            }
        });

        Console.WriteLine("ĐÃ TẠO THÀNH CÔNG TOÀN BỘ 7 ẢNH CHỤP MÀN HÌNH CHẤT LƯỢNG CAO!");
    }

    /// <summary>
    /// Bọc giao diện trong một khung cửa sổ Windows 11 hoàn chỉnh với tiêu đề và các nút điều khiển thu nhỏ/phóng to/đóng.
    /// </summary>
    private static void SaveElement(Window window, string title, string filePath, int width, int height, Action<Window>? prepare = null)
    {
        var dataContext = window.DataContext;
        window.Show();
        DoEvents();

        prepare?.Invoke(window);
        DoEvents();

        // Lấy nội dung chính của cửa sổ
        var content = window.Content as FrameworkElement;
        window.Content = null; // Gỡ khỏi window cũ để gắn vào container ảnh

        var container = new Border
        {
            Width = width,
            Height = height,
            Background = (Brush)System.Windows.Application.Current.Resources["WindowBackgroundBrush"],
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            DataContext = dataContext
        };

        var rootGrid = new Grid { DataContext = dataContext };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) }); // Title bar
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Nội dung

        // Tạo Windows 11 Title Bar
        var titleBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 228, 232)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        Grid.SetColumn(titleText, 0);
        titleGrid.Children.Add(titleText);

        // Nút điều khiển [ - ] [ □ ] [ ✕ ]
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        buttonsPanel.Children.Add(CreateCaptionButton("—", 10));
        buttonsPanel.Children.Add(CreateCaptionButton("□", 11));
        buttonsPanel.Children.Add(CreateCaptionButton("✕", 11, isClose: true));

        Grid.SetColumn(buttonsPanel, 1);
        titleGrid.Children.Add(buttonsPanel);
        titleBar.Child = titleGrid;

        Grid.SetRow(titleBar, 0);
        rootGrid.Children.Add(titleBar);

        if (content != null)
        {
            content.DataContext = dataContext;
            Grid.SetRow(content, 1);
            rootGrid.Children.Add(content);
        }

        container.Child = rootGrid;

        // Render ra bitmap
        container.Measure(new Size(width, height));
        container.Arrange(new Rect(0, 0, width, height));
        container.UpdateLayout();
        DoEvents();

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(container);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using (var fs = File.Create(filePath))
        {
            encoder.Save(fs);
        }

        // Trả lại content cho window cũ để dọn dẹp sạch sẽ
        rootGrid.Children.Remove(content);
        window.Content = content;
        window.Close();
    }

    private static Border CreateCaptionButton(string text, double fontSize, bool isClose = false)
    {
        var btn = new Border
        {
            Width = 46,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        btn.Child = tb;
        return btn;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var desc = FindVisualChild<T>(child);
            if (desc != null) return desc;
        }
        return null;
    }

    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
        Dispatcher.PushFrame(frame);
    }

    private static object? ExitFrame(object frame)
    {
        ((DispatcherFrame)frame).Continue = false;
        return null;
    }
}
