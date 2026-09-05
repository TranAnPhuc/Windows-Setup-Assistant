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
    public static bool TryValidate(string? packageId, out string error)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            error = "Package Id không được để trống.";
            return false;
        }

        // Người dùng hay copy/paste kèm khoảng trắng thừa - bỏ trước rồi mới kiểm tra.
        packageId = packageId.Trim();

        if (packageId.Length > MaxLength)
        {
            error = $"Package Id quá dài (tối đa {MaxLength} ký tự).";
            return false;
        }

        if (packageId.StartsWith('-'))
        {
            error = "Package Id không được bắt đầu bằng dấu '-' vì sẽ bị hiểu nhầm là tham số dòng lệnh.";
            return false;
        }

        if (!PackageIdPattern().IsMatch(packageId))
        {
            error = "Package Id chỉ được chứa chữ, số và các ký tự . _ - + (ví dụ: Google.Chrome).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>Ném <see cref="ArgumentException"/> nếu Id không hợp lệ. Dùng ngay trước khi chạy winget.</summary>
    public static string EnsureValid(string? packageId)
    {
        if (!TryValidate(packageId, out var error))
        {
            throw new ArgumentException(error, nameof(packageId));
        }

        return packageId!.Trim();
    }
}
