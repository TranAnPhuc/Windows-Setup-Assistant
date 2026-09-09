using System.IO;
using System.Runtime.InteropServices;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Mượn cửa sổ lệnh của tiến trình cha để in chữ ra.
///
/// Ứng dụng được build ở dạng WinExe nên KHÔNG có console riêng. AttachConsole(-1) gắn vào
/// console của tiến trình cha - tức đúng cửa sổ cmd/PowerShell mà người dùng đang gõ.
/// Khi không có cha nào có console (bấm đúp vào .exe, chạy từ Task Scheduler) thì hàm này
/// thất bại: đó KHÔNG phải lỗi, ứng dụng vẫn chạy và vẫn ghi báo cáo, chỉ là không in ra đâu.
/// </summary>
public sealed class ConsoleSession : IUnattendedOutput, IDisposable
{
    private const int AttachParentProcess = -1;

    private readonly bool _attached;
    private bool _disposed;

    private ConsoleSession(bool attached) => _attached = attached;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    public bool IsAttached => _attached;

    public static ConsoleSession Attach()
    {
        bool attached;

        try
        {
            attached = AttachConsole(AttachParentProcess);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            attached = false;
        }

        if (attached)
        {
            RebindStandardStreams();
        }

        return new ConsoleSession(attached);
    }

    /// <summary>
    /// BẮT BUỘC sau khi gắn console. Tiến trình WinExe khởi động với Console.Out trỏ vào
    /// chỗ trống; không gán lại thì AttachConsole báo thành công nhưng không chữ nào hiện ra.
    /// </summary>
    private static void RebindStandardStreams()
    {
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch (IOException)
        {
            // Console cha đã đóng giữa chừng - không in được nhưng cũng không được làm sập.
        }
    }

    public void WriteLine(string text = "")
    {
        if (!_attached || _disposed)
        {
            return;
        }

        try
        {
            Console.Out.WriteLine(text);
        }
        catch (IOException)
        {
        }
    }

    public void WriteError(string text)
    {
        if (!_attached || _disposed)
        {
            return;
        }

        try
        {
            Console.Error.WriteLine(text);
        }
        catch (IOException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_attached)
        {
            return;
        }

        try
        {
            Console.Out.Flush();
            Console.Error.Flush();
            FreeConsole();
        }
        catch (IOException)
        {
        }
    }
}
