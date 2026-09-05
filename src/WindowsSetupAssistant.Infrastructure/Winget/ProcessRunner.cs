using System.Diagnostics;
using System.Text;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;

namespace WindowsSetupAssistant.Infrastructure.Winget;

/// <summary>
/// Chạy tiến trình ngoài một cách an toàn và bất đồng bộ.
///
/// Ba điểm quan trọng về BẢO MẬT và ỔN ĐỊNH:
/// 1. Dùng <see cref="ProcessStartInfo.ArgumentList"/> - mỗi tham số là một phần tử riêng,
///    Windows sẽ tự escape. Không bao giờ ghép chuỗi lệnh nên không thể bị command injection.
/// 2. UseShellExecute = false - không đi qua cmd.exe nên các ký tự như &amp; | &gt; vô hại.
/// 3. Có timeout và CancellationToken - winget treo hay người dùng bấm Huỷ đều xử lý được,
///    tiến trình con bị kill theo cả cây tiến trình.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var commandText = FormatCommand(fileName, arguments);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stdout.AppendLine(e.Data);
            outputProgress?.Report(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stderr.AppendLine(e.Data);
        };

        // Không khởi chạy tiến trình (đặc biệt installer) nếu yêu cầu đã bị huỷ.
        cancellationToken.ThrowIfCancellationRequested();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource();
        if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(timeout.Value);
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var timedOut = false;

        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);

            // Phân biệt "người dùng huỷ" và "winget treo quá lâu".
            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                throw new OperationCanceledException(cancellationToken);
            }

            timedOut = true;
        }

        // WaitForExit() không tham số bảo đảm các stream stdout/stderr đã được đọc hết.
        try
        {
            process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // Tiến trình đã bị giải phóng - bỏ qua.
        }

        stopwatch.Stop();

        return new ProcessRunResult
        {
            Command = commandText,
            ExitCode = timedOut ? -1 : SafeExitCode(process),
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            Duration = stopwatch.Elapsed,
            TimedOut = timedOut
        };
    }

    private static int SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception)
        {
            // Tiến trình có thể vừa tự thoát - không cần xử lý thêm.
        }
    }

    /// <summary>
    /// Ghép chuỗi lệnh CHỈ để hiển thị trong nhật ký (không dùng để thực thi).
    /// </summary>
    public static string FormatCommand(string fileName, IReadOnlyList<string> arguments)
    {
        var parts = new List<string> { QuoteIfNeeded(fileName) };
        parts.AddRange(arguments.Select(QuoteIfNeeded));
        return string.Join(' ', parts);
    }

    private static string QuoteIfNeeded(string value) =>
        value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
}
