using WindowsSetupAssistant.Domain.Localization;
using System.Text.RegularExpressions;

namespace WindowsSetupAssistant.Domain.Validation;

/// <summary>
/// Kiểm tra WinGet Package Id trước khi đưa vào tiến trình winget.
/// Đây là lớp phòng thủ thứ hai: dù chúng ta đã dùng ArgumentList (không qua shell),
/// vẫn phải chặn chuỗi lạ để tránh việc dữ liệu người dùng bị hiểu thành tham số dòng lệnh.
/// </summary>
public static partial class PackageIdValidator
{
    public const int MaxLength = 200;

    /// <summary>
    /// Id hợp lệ: bắt đầu bằng chữ/số, phần còn lại chỉ gồm chữ, số và . _ - +
    /// Ví dụ hợp lệ: Google.Chrome, 7zip.7zip, Notepad++.Notepad++, mcmilk.7zip-zstd
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._+\-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdPattern();

    public static bool IsValid(string? packageId) => TryValidate(packageId, out _);

    /// <summary>Trả về true nếu hợp lệ; ngược lại trả về false kèm thông báo lỗi tiếng Việt.</summary>
    public static bool TryValidate(string? packageId, out LocalizedText error)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            error = LocalizedText.Of(MessageKeys.PackageIdEmpty);
            return false;
        }

        packageId = packageId.Trim();

        if (packageId.Length > MaxLength)
        {
            error = LocalizedText.Of(MessageKeys.PackageIdTooLong, MaxLength);
            return false;
        }

        if (packageId.StartsWith('-'))
        {
            error = LocalizedText.Of(MessageKeys.PackageIdStartsWithDash);
            return false;
        }

        if (!PackageIdPattern().IsMatch(packageId))
        {
            error = LocalizedText.Of(MessageKeys.PackageIdInvalidCharacters);
            return false;
        }

        error = LocalizedText.Raw(string.Empty);
        return true;
    }

    public static string EnsureValid(string? packageId)
    {
        if (!TryValidate(packageId, out var error))
        {
            throw new LocalizedException(error);
        }

        return packageId!.Trim();
    }
}
