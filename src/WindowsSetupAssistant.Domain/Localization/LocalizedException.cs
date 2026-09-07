namespace WindowsSetupAssistant.Domain.Localization;

/// <summary>
/// Ngoại lệ mang theo thông điệp dành cho người dùng dưới dạng khoá.
///
/// Message của Exception giữ nguyên khoá - đủ dùng cho log kỹ thuật và debug,
/// còn nơi hiển thị sẽ đọc LocalizedMessage rồi dịch.
/// </summary>
public class LocalizedException : Exception
{
    public LocalizedException(LocalizedText message)
        : base(message?.Key ?? string.Empty)
    {
        ArgumentNullException.ThrowIfNull(message);
        LocalizedMessage = message;
    }

    public LocalizedException(LocalizedText message, Exception innerException)
        : base(message?.Key ?? string.Empty, innerException)
    {
        ArgumentNullException.ThrowIfNull(message);
        LocalizedMessage = message;
    }

    public LocalizedText LocalizedMessage { get; }
}
