using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Nhật ký ứng dụng: vừa hiển thị trên màn hình Nhật ký, vừa ghi ra file.
/// </summary>
public interface IAppLogger
{
    /// <summary>Bắn ra mỗi khi có dòng log mới (ViewModel lắng nghe để cập nhật UI).</summary>
    event EventHandler<LogEntry>? EntryLogged;

    void Log(LogEntry entry);

    void Information(string message, string? command = null, string? details = null);

    void Warning(string message, string? command = null, string? details = null);

    void Error(string message, string? command = null, int? exitCode = null, string? details = null);
}

/// <summary>Các hàm mở rộng tiện dụng cho <see cref="IAppLogger"/>.</summary>
public static class AppLoggerExtensions
{
    public static void LogCommand(this IAppLogger logger, string command, int exitCode, string? details = null)
    {
        logger.Log(new LogEntry
        {
            Level = exitCode == 0 ? LogLevel.Information : LogLevel.Error,
            Message = exitCode == 0 ? "Lệnh chạy thành công." : "Lệnh kết thúc với lỗi.",
            Command = command,
            ExitCode = exitCode,
            Details = details
        });
    }
}
