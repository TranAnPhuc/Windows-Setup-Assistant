namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Nơi DUY NHẤT liệt kê khoá chuỗi giao diện.
/// Mỗi khi chuyển một chuỗi cứng sang khoá, thêm hằng vào đây và thêm giá trị vào cả 3 file .resx.
/// Test đối chiếu ở ResourceParityTests sẽ bắt ngay nếu quên một ngôn ngữ.
/// </summary>
public static class UiKeys
{
    // --- Thanh tiêu đề ---
    public const string AppTitle = "Ui_AppTitle";
    public const string AppSubtitle = "Ui_AppSubtitle";
    public const string ProfileLabel = "Ui_ProfileLabel";
    public const string ProfileNew = "Ui_ProfileNew";
    public const string ProfileRename = "Ui_ProfileRename";
    public const string ProfileDuplicate = "Ui_ProfileDuplicate";
    public const string ProfileDelete = "Ui_ProfileDelete";
    public const string ThemeDark = "Ui_ThemeDark";
    public const string ThemeLight = "Ui_ThemeLight";
    public const string LanguageLabel = "Ui_LanguageLabel";
}
