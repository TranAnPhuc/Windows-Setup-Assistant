using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Models;

/// <summary>
/// Một dòng nhật ký hiển thị ở màn hình Nhật ký và ghi ra file log.
/// </summary>
public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public LogLevel Level { get; init; } = LogLevel.Information;

    public string Message { get; init; } = string.Empty;

    /// <summary>Câu lệnh liên quan (nếu có), ví dụ: winget install --id Git.Git --exact ...</summary>
    public string? Command { get; init; }

    public int? ExitCode { get; init; }

    public string? Details { get; init; }

    public override string ToString()
    {
        var parts = new List<string>
        {
            $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level.ToString().ToUpperInvariant()}] {Message}"
        };

        if (!string.IsNullOrWhiteSpace(Command))
        {
            parts.Add($"    > {Command}");
        }

        if (ExitCode.HasValue)
        {
            parts.Add($"    exit code: {ExitCode.Value} (0x{ExitCode.Value:X8})");
        }

        if (!string.IsNullOrWhiteSpace(Details))
        {
            parts.Add($"    {Details.Replace("\n", "\n    ")}");
        }

        return string.Join(Environment.NewLine, parts);
    }
}
