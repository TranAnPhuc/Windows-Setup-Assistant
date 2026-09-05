namespace WindowsSetupAssistant.Domain.Enums;

/// <summary>
/// Hành vi khi phần mềm đã tồn tại trên máy.
/// </summary>
public enum ExistingPackageAction
{
    /// <summary>Bỏ qua, không đụng tới gói đã cài.</summary>
    Skip = 0,

    /// <summary>Chạy "winget upgrade" để nâng cấp nếu có bản mới.</summary>
    Upgrade = 1
}
