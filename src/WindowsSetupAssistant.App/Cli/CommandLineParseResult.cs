namespace WindowsSetupAssistant.App.Cli;

/// <summary>Ứng dụng phải làm gì sau khi đọc xong tham số.</summary>
public enum CommandLineMode
{
    /// <summary>Mở giao diện đồ hoạ như bình thường.</summary>
    Gui,

    /// <summary>Chạy lượt cài đặt không giám sát.</summary>
    Unattended,

    /// <summary>In hướng dẫn rồi thoát.</summary>
    Help,

    /// <summary>Tham số sai: in lỗi kèm hướng dẫn rồi thoát mã 2.</summary>
    Invalid
}

/// <summary>Kết quả phân tích tham số. Bất biến, không có I/O.</summary>
public sealed class CommandLineParseResult
{
    private CommandLineParseResult(CommandLineMode mode) => Mode = mode;

    public CommandLineMode Mode { get; }

    /// <summary>Chỉ khác null khi <see cref="Mode"/> là <see cref="CommandLineMode.Unattended"/>.</summary>
    public CommandLineOptions? Options { get; private init; }

    /// <summary>Chỉ khác null khi <see cref="Mode"/> là <see cref="CommandLineMode.Invalid"/>.</summary>
    public string? ErrorMessage { get; private init; }

    public static CommandLineParseResult Gui() => new(CommandLineMode.Gui);

    public static CommandLineParseResult Help() => new(CommandLineMode.Help);

    public static CommandLineParseResult Unattended(CommandLineOptions options) =>
        new(CommandLineMode.Unattended) { Options = options };

    public static CommandLineParseResult Invalid(string message) =>
        new(CommandLineMode.Invalid) { ErrorMessage = message };
}
