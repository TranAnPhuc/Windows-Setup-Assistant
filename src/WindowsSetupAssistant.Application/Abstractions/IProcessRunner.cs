using WindowsSetupAssistant.Application.Models;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Trừu tượng hoá việc chạy tiến trình ngoài, để unit test không cần gọi winget thật.
/// Tham số luôn được truyền dưới dạng danh sách (ArgumentList) chứ không ghép chuỗi.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);
}
