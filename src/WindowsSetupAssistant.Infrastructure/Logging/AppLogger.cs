using System.Globalization;
using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Infrastructure.Logging;

/// <summary>
/// Nhật ký đơn giản: bắn sự kiện cho UI và ghi thêm ra file text cạnh ứng dụng.
///
/// Nguyên tắc: logger KHÔNG BAO GIỜ được ném lỗi ra ngoài. Nếu ghi file thất bại
/// (thư mục chỉ đọc, USB bị rút...) thì bỏ qua, ứng dụng vẫn phải chạy tiếp.
///
/// Sự kiện <see cref="EntryLogged"/> có thể được bắn từ luồng nền,
/// nên phía ViewModel phải chuyển về UI thread trước khi cập nhật giao diện.
/// </summary>
public sealed class AppLogger : IAppLogger
{
    private static readonly CultureInfo FileLogCulture = CultureInfo.GetCultureInfo("en");

    private readonly object _fileLock = new();
    private readonly string? _logFilePath;
    private readonly IStringLocalizer _localizer;

    public AppLogger(string? logDirectory, bool writeToFile, IStringLocalizer localizer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

        if (!writeToFile)
        {
            return;
        }

        try
        {
            var directory = string.IsNullOrWhiteSpace(logDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Logs")
                : logDirectory;

            Directory.CreateDirectory(directory);
            _logFilePath = Path.Combine(directory, $"setup-assistant-{DateTime.Now:yyyyMMdd}.log");
        }
        catch (Exception)
        {
            _logFilePath = null;
        }
    }

    public AppLogger(string? logDirectory = null, bool writeToFile = true)
        : this(logDirectory, writeToFile, FallbackLocalizer.Instance)
    {
    }

    public event EventHandler<LogEntry>? EntryLogged;

    /// <summary>Đường dẫn file log hiện tại (null nếu không ghi được file).</summary>
    public string? LogFilePath => _logFilePath;

    public void Log(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        WriteToFile(entry);

        try
        {
            EntryLogged?.Invoke(this, entry);
        }
        catch (Exception)
        {
            // Không để lỗi phía người nghe làm hỏng luồng nghiệp vụ.
        }
    }

    public void Information(LocalizedText message, string? command = null, string? details = null) =>
        Log(new LogEntry { Level = LogLevel.Information, Message = message, Command = command, Details = details });

    public void Warning(LocalizedText message, string? command = null, string? details = null) =>
        Log(new LogEntry { Level = LogLevel.Warning, Message = message, Command = command, Details = details });

    public void Error(LocalizedText message, string? command = null, int? exitCode = null, string? details = null) =>
        Log(new LogEntry
        {
            Level = LogLevel.Error,
            Message = message,
            Command = command,
            ExitCode = exitCode,
            Details = details
        });

    public void Information(string message, string? command = null, string? details = null) =>
        Information(LocalizedText.Raw(message), command, details);

    public void Warning(string message, string? command = null, string? details = null) =>
        Warning(LocalizedText.Raw(message), command, details);

    public void Error(string message, string? command = null, int? exitCode = null, string? details = null) =>
        Error(LocalizedText.Raw(message), command, exitCode, details);

    private string Render(LogEntry entry)
    {
        var parts = new List<string>
        {
            $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level.ToString().ToUpperInvariant()}] " +
            _localizer.Format(entry.Message, FileLogCulture)
        };

        if (!string.IsNullOrWhiteSpace(entry.Command))
        {
            parts.Add($"    > {entry.Command}");
        }

        if (entry.ExitCode.HasValue)
        {
            parts.Add($"    exit code: {entry.ExitCode.Value} (0x{entry.ExitCode.Value:X8})");
        }

        if (!string.IsNullOrWhiteSpace(entry.Details))
        {
            parts.Add($"    {entry.Details.Replace("\n", "\n    ")}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private void WriteToFile(LogEntry entry)
    {
        if (_logFilePath is null)
        {
            return;
        }

        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, Render(entry) + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Ghi log lỗi thì cũng chỉ bỏ qua - không có gì để làm thêm.
        }
    }
}

internal sealed class FallbackLocalizer : IStringLocalizer
{
    public static readonly FallbackLocalizer Instance = new();

    public string this[string key] => key;

    public string Format(LocalizedText text) => Format(text, CultureInfo.InvariantCulture);

    public string Format(LocalizedText text, CultureInfo culture) =>
        text.IsRaw ? text.Key : (text.Arguments.Count > 0 ? string.Format(culture, text.Key, text.Arguments.ToArray()) : text.Key);
}
