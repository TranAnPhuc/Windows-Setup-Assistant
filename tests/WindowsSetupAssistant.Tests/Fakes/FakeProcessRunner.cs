using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;

namespace WindowsSetupAssistant.Tests.Fakes;

/// <summary>
/// Thay thế tiến trình winget thật trong unit test.
/// Ghi lại toàn bộ tham số được truyền vào để kiểm tra câu lệnh có đúng và an toàn hay không.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public List<RecordedCall> Calls { get; } = new();

    /// <summary>Cho phép mỗi test tự quyết định kết quả trả về dựa trên tham số.</summary>
    public Func<string, IReadOnlyList<string>, ProcessRunResult>? Handler { get; set; }

    public RecordedCall LastCall => Calls[^1];

    public Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var call = new RecordedCall(fileName, arguments.ToList(), timeout);
        Calls.Add(call);

        var result = Handler?.Invoke(fileName, arguments) ?? Success(string.Empty);
        return Task.FromResult(result);
    }

    public static ProcessRunResult Success(string standardOutput) => new()
    {
        Command = "winget (fake)",
        ExitCode = 0,
        StandardOutput = standardOutput
    };

    public static ProcessRunResult WithExitCode(int exitCode, string standardOutput = "") => new()
    {
        Command = "winget (fake)",
        ExitCode = exitCode,
        StandardOutput = standardOutput
    };

    public static ProcessRunResult Timeout() => new()
    {
        Command = "winget (fake)",
        ExitCode = -1,
        TimedOut = true
    };

    public sealed record RecordedCall(string FileName, IReadOnlyList<string> Arguments, TimeSpan? Timeout)
    {
        public string Verb => Arguments.Count > 0 ? Arguments[0] : string.Empty;

        /// <summary>Lấy giá trị đứng ngay sau một tham số, ví dụ ValueOf("--id") -> "Google.Chrome".</summary>
        public string? ValueOf(string flag)
        {
            for (var i = 0; i < Arguments.Count - 1; i++)
            {
                if (Arguments[i] == flag)
                {
                    return Arguments[i + 1];
                }
            }

            return null;
        }

        public bool Has(string flag) => Arguments.Contains(flag);
    }
}
