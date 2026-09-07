using System.Globalization;
using System.Resources;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Đọc bản dịch từ .resx. Đây là NGUỒN SỰ THẬT của việc dịch;
/// LocalizationSource chỉ là lớp bọc phục vụ binding trong XAML.
/// </summary>
public sealed class ResourceStringLocalizer : IStringLocalizer
{
    private readonly ResourceManager _resourceManager = new(
        "WindowsSetupAssistant.App.Localization.Strings",
        typeof(ResourceStringLocalizer).Assembly);

    public string this[string key] => Get(key, CultureInfo.CurrentUICulture);

    public string Format(LocalizedText text) => Format(text, CultureInfo.CurrentUICulture);

    public string Format(LocalizedText text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);

        // Văn bản Raw là đường dẫn, tên phần mềm, output winget - in nguyên văn.
        if (text.IsRaw)
        {
            return text.Key;
        }

        var pattern = Get(text.Key, culture);

        if (text.Arguments.Count == 0)
        {
            return pattern;
        }

        try
        {
            return string.Format(culture, pattern, text.Arguments.ToArray());
        }
        catch (FormatException)
        {
            // Bản dịch sai số tham số - trả chuỗi chưa điền còn hơn làm sập ứng dụng.
            return pattern;
        }
    }

    private string Get(string key, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        try
        {
            // Khoá không có bản dịch thì trả về chính khoá - rất dễ nhận ra khi kiểm thử.
            return _resourceManager.GetString(key, culture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }
}
