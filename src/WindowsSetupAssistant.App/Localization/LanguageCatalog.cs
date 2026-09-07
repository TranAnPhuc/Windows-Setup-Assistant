using System.Globalization;

namespace WindowsSetupAssistant.App.Localization;

/// <param name="Code">Mã culture dùng cho .resx.</param>
/// <param name="DisplayName">Tên hiển thị, luôn viết bằng chính ngôn ngữ đó.</param>
public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Ba ngôn ngữ được hỗ trợ và cách chọn ngôn ngữ ở lần chạy đầu.</summary>
public static class LanguageCatalog
{
    public const string English = "en";
    public const string Vietnamese = "vi";
    public const string TraditionalChinese = "zh-Hant";

    public static IReadOnlyList<LanguageOption> Supported { get; } = new[]
    {
        new LanguageOption(Vietnamese, "Tiếng Việt"),
        new LanguageOption(English, "English"),
        new LanguageOption(TraditionalChinese, "繁體中文")
    };

    /// <summary>
    /// Dò ngôn ngữ theo Windows. Mọi biến thể tiếng Trung đều về phồn thể vì đây là
    /// bản duy nhất ứng dụng có - thà hiện chữ Hán còn hơn rơi về tiếng Anh.
    /// </summary>
    public static string Detect(CultureInfo installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        var name = installed.Name;

        if (name.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
        {
            return Vietnamese;
        }

        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return TraditionalChinese;
        }

        return English;
    }

    /// <summary>Giá trị đã lưu được ưu tiên; không hợp lệ thì dò lại theo Windows.</summary>
    public static CultureInfo Resolve(string? savedCode, CultureInfo installed)
    {
        var code = Supported.Any(l => string.Equals(l.Code, savedCode, StringComparison.OrdinalIgnoreCase))
            ? savedCode!
            : Detect(installed);

        return CultureInfo.GetCultureInfo(code);
    }
}
