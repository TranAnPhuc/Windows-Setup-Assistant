namespace WindowsSetupAssistant.Domain.Localization;

/// <summary>
/// Một thông điệp dành cho người dùng, biểu diễn bằng KHOÁ chứ không phải câu chữ.
///
/// Nhờ vậy tầng Domain, Application và Infrastructure không cần biết ứng dụng đang chạy
/// bằng ngôn ngữ nào - việc dịch là chuyện của tầng giao diện.
/// </summary>
public sealed class LocalizedText
{
    private static readonly object?[] NoArguments = Array.Empty<object?>();

    public static Func<LocalizedText, string>? DefaultFormatter { get; set; }

    private LocalizedText(string key, object?[] arguments, bool isRaw)
    {
        Key = key;
        Arguments = arguments;
        IsRaw = isRaw;
    }

    /// <summary>Khoá tra trong .resx. Với văn bản Raw thì đây chính là nội dung.</summary>
    public string Key { get; }

    /// <summary>Tham số điền vào chuỗi định dạng.</summary>
    public IReadOnlyList<object?> Arguments { get; }

    /// <summary>True nghĩa là không dịch, in nguyên văn.</summary>
    public bool IsRaw { get; }

    public static LocalizedText Of(string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new LocalizedText(key, arguments.Length == 0 ? NoArguments : arguments, isRaw: false);
    }

    /// <summary>
    /// Văn bản không dịch được và không nên dịch: tên phần mềm, đường dẫn file,
    /// output nguyên văn của winget.
    /// </summary>
    public static LocalizedText Raw(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LocalizedText(text, NoArguments, isRaw: true);
    }

    public override string ToString() => DefaultFormatter?.Invoke(this) ?? Key;
}
