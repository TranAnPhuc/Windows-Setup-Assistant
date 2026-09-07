using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Domain.Localization;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.App.Services;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>
/// Màn hình Nhật ký: hiển thị câu lệnh, thời gian, exit code và thông báo lỗi.
///
/// Điểm kỹ thuật quan trọng: logger bắn sự kiện từ luồng nền, trong khi
/// ObservableCollection chỉ được phép thay đổi trên UI thread.
/// Vì vậy phải chuyển về Dispatcher trước khi thêm dòng log.
/// </summary>
public sealed class LogViewModel : ObservableObject, IDisposable
{
    private const int MaxEntries = 2000;

    private readonly IDialogService _dialogService;
    private readonly IAppLogger _logger;
    private readonly string? _logFilePath;
    private bool _autoScroll = true;
    private bool _disposed;

    private readonly IStringLocalizer _localizer;

    public LogViewModel(IAppLogger logger, IDialogService dialogService, string? logFilePath, IStringLocalizer localizer)
    {
        _dialogService = dialogService;
        _logger = logger;
        _logFilePath = logFilePath;
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

        logger.EntryLogged += OnEntryLogged;

        ClearCommand = new RelayCommand(() => Entries.Clear());
        CopyCommand = new RelayCommand(CopyToClipboard);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder, () => _logFilePath is not null);
    }

    public LogViewModel(IAppLogger logger, IDialogService dialogService, string? logFilePath)
        : this(logger, dialogService, logFilePath, LocalizationSource.Instance.Localizer)
    {
    }

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public RelayCommand ClearCommand { get; }

    public RelayCommand CopyCommand { get; }

    public RelayCommand OpenLogFolderCommand { get; }

    public string LogFileDescription => _logFilePath is null
        ? _localizer[UiKeys.LogFileCannotWrite]
        : _localizer.Format(LocalizedText.Of(UiKeys.LogFileDescription, _logFilePath));

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    private void OnEntryLogged(object? sender, LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        var dispatcher = WpfApplication.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Add(entry);
        }
        else if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
        {
            dispatcher.BeginInvoke(() => Add(entry));
        }
    }

    private void Add(LogEntry entry)
    {
        if (_disposed)
        {
            return;
        }

        Entries.Add(entry);

        // Giới hạn số dòng để chạy lâu không ngốn hết bộ nhớ.
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }
    }

    private void CopyToClipboard()
    {
        try
        {
            var text = string.Join(Environment.NewLine, Entries.Select(e => e.ToString()));
            Clipboard.SetText(text);
            _dialogService.ShowInfo(_localizer[UiKeys.LogDialogTitle], _localizer[UiKeys.LogCopied]);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(_localizer[UiKeys.LogDialogTitle], _localizer.Format(LocalizedText.Of(UiKeys.LogCopyFailed, ex.Message)));
        }
    }

    private void OpenLogFolder()
    {
        if (_logFilePath is null)
        {
            return;
        }

        try
        {
            var folder = Path.GetDirectoryName(_logFilePath);
            if (folder is null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(_localizer[UiKeys.LogDialogTitle], _localizer.Format(LocalizedText.Of(UiKeys.LogOpenFolderFailed, ex.Message)));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger.EntryLogged -= OnEntryLogged;
    }
}
