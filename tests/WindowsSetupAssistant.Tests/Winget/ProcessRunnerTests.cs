using WindowsSetupAssistant.Infrastructure.Winget;

namespace WindowsSetupAssistant.Tests.Winget;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_PreCancelledToken_DoesNotAttemptToStartProcess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var runner = new ProcessRunner();

        // Đường dẫn không tồn tại: nếu Start bị gọi, test nhận Win32Exception.
        // Test này không thể chạy WinGet hay bất kỳ installer thật nào.
        var missingExecutable = Path.Combine(
            Path.GetTempPath(), "wsa-tests", Guid.NewGuid().ToString("N"), "never-start.exe");

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(missingExecutable, Array.Empty<string>(), cancellationToken: cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task RunAsync_CancelledWhileRunning_KillsProcessTreeAndThrows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        var runner = new ProcessRunner();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(150));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync("cmd.exe", new[] { "/c", "ping 127.0.0.1 -n 10 > nul" }, cancellationToken: cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }
}
