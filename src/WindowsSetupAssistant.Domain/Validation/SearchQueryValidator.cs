using WindowsSetupAssistant.Domain.Localization;
namespace WindowsSetupAssistant.Domain.Validation;

/// <summary>
/// Kiểm tra từ khoá tìm kiếm trước khi truyền cho winget.
/// Từ khoá là văn bản tự do nên chỉ cần chặn ký tự điều khiển và tiền tố '-'.
/// </summary>
public static class SearchQueryValidator
{
    public const int MaxLength = 100;

    public static bool TryValidate(string? query, out LocalizedText error)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            error = LocalizedText.Of(MessageKeys.SearchQueryEmpty);
            return false;
        }

        var trimmed = query.Trim();

        if (trimmed.Length > MaxLength)
        {
            error = LocalizedText.Of(MessageKeys.SearchQueryTooLong, MaxLength);
            return false;
        }

        if (trimmed.StartsWith('-'))
        {
            error = LocalizedText.Of(MessageKeys.SearchQueryStartsWithDash);
            return false;
        }

        if (trimmed.Any(char.IsControl))
        {
            error = LocalizedText.Of(MessageKeys.SearchQueryControlCharacters);
            return false;
        }

        error = LocalizedText.Raw(string.Empty);
        return true;
    }

    public static string EnsureValid(string? query)
    {
        if (!TryValidate(query, out var error))
        {
            throw new LocalizedException(error);
        }

        return query!.Trim();
    }
}
