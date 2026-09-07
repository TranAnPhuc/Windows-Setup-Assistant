using System.Globalization;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Hợp đồng dịch. Khai báo ở tầng Application chứ không phải App vì Infrastructure
/// cũng cần nó: AppLogger phải dịch sang tiếng Anh khi ghi file log.
/// </summary>
public interface IStringLocalizer
{
    /// <summary>Lấy chuỗi theo khoá. Khoá không tồn tại thì trả về chính khoá đó.</summary>
    string this[string key] { get; }

    /// <summary>Dịch và điền tham số theo ngôn ngữ đang dùng.</summary>
    string Format(LocalizedText text);

    /// <summary>Dịch theo một ngôn ngữ chỉ định.</summary>
    string Format(LocalizedText text, CultureInfo culture);
}
