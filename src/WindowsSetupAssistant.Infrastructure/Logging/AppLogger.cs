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
    private readonly object _fileLock = new();
    private readonly string? _logFilePath;

    public AppLogger(string? logDirectory = null, bool writeToFile = true)
    {
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

    public void Information(string message, string? command = null, string? details = null) =>
        Log(new LogEntry { Level = LogLevel.Information, Message = message, Command = command, Details = details });

    public void Warning(string message, string? command = null, string? details = null) =>
        Log(new LogEntry { Level = LogLevel.Warning, Message = message, Command = command, Details = details });

    public void Error(string message, string? command = null, int? exitCode = null, string? details = null) =>
        Log(new LogEntry
        {
            Level = LogLevel.Error,
            Message = message,
            Command = command,
            ExitCode = exitCode,
            Details = details
        });

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
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Ghi log lỗi thì cũng chỉ bỏ qua - không có gì để làm thêm.
        }
    }
}
