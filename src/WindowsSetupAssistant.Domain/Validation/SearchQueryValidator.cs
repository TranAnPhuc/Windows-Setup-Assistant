namespace WindowsSetupAssistant.Domain.Validation;

/// <summary>
/// Kiểm tra từ khoá tìm kiếm trước khi truyền cho winget.
/// Từ khoá là văn bản tự do nên chỉ cần chặn ký tự điều khiển và tiền tố '-'.
/// </summary>
public static class SearchQueryValidator
{
    public const int MaxLength = 100;

    public static bool TryValidate(string? query, out string error)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            error = "Từ khoá tìm kiếm không được để trống.";
            return false;
        }

        var trimmed = query.Trim();

        if (trimmed.Length > MaxLength)
        {
            error = $"Từ khoá quá dài (tối đa {MaxLength} ký tự).";
            return false;
        }

        if (trimmed.StartsWith('-'))
        {
            error = "Từ khoá không được bắt đầu bằng dấu '-'.";
            return false;
        }

        if (trimmed.Any(char.IsControl))
        {
            error = "Từ khoá chứa ký tự điều khiển không hợp lệ.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static string EnsureValid(string? query)
    {
        if (!TryValidate(query, out var error))
        {
            throw new ArgumentException(error, nameof(query));
        }

        return query!.Trim();
    }
}
