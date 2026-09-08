namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Nơi chế độ không giám sát ghi chữ ra. Tách thành interface để test kiểm chứng được
/// nội dung mà không cần console thật (ConsoleSession dùng P/Invoke, không chạy trong test runner).
/// </summary>
public interface IUnattendedOutput
{
    void WriteLine(string text = "");

    void WriteError(string text);
}
