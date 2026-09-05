namespace WindowsSetupAssistant.Domain.Enums;

/// <summary>
/// Trạng thái cài đặt hiện tại của một phần mềm trên máy (thông tin runtime, không lưu vào JSON).
/// </summary>
public enum InstallState
{
    Unknown = 0,
    Checking = 1,
    NotInstalled = 2,
    Installed = 3
}
