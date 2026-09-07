using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Models;

/// <summary>
/// Một dòng nhật ký hiển thị ở màn hình Nhật ký và ghi ra file log.
/// </summary>
public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public LogLevel Level { get; init; } = LogLevel.Information;

    public LocalizedText Message { get; init; } = LocalizedText.Raw(string.Empty);

    /// <summary>Câu lệnh liên quan (nếu có), ví dụ: winget install --id Git.Git --exact ...</summary>
    public string? Command { get; init; }

    public int? ExitCode { get; init; }

    public string? Details { get; init; }


}
