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

    // --- MainWindow UI keys ---
    public const string ProfileTooltip = "Ui_ProfileTooltip";
    public const string ProfileNewTooltip = "Ui_ProfileNewTooltip";
    public const string WarningWingetUnavailable = "Ui_WarningWingetUnavailable";
    public const string WarningWingetHelp = "Ui_WarningWingetHelp";
    public const string BtnOpenStore = "Ui_BtnOpenStore";
    public const string TabSoftwareList = "Ui_TabSoftwareList";
    public const string FilterPlaceholder = "Ui_FilterPlaceholder";
    public const string BtnSelectAll = "Ui_BtnSelectAll";
    public const string BtnSelectNone = "Ui_BtnSelectNone";
    public const string BtnInvertSelection = "Ui_BtnInvertSelection";
    public const string BtnSelectNotInstalled = "Ui_BtnSelectNotInstalled";
    public const string BtnAdd = "Ui_BtnAdd";
    public const string BtnEdit = "Ui_BtnEdit";
    public const string BtnDelete = "Ui_BtnDelete";
    public const string BtnMoveUp = "Ui_BtnMoveUp";
    public const string BtnMoveUpTooltip = "Ui_BtnMoveUpTooltip";
    public const string BtnMoveDown = "Ui_BtnMoveDown";
    public const string BtnMoveDownTooltip = "Ui_BtnMoveDownTooltip";
    public const string ColumnInstall = "Ui_ColumnInstall";
    public const string ColumnInstallTooltip = "Ui_ColumnInstallTooltip";
    public const string ColumnName = "Ui_ColumnName";
    public const string ColumnPackageId = "Ui_ColumnPackageId";
    public const string ColumnCategory = "Ui_ColumnCategory";
    public const string ColumnStatus = "Ui_ColumnStatus";
    public const string ColumnNotes = "Ui_ColumnNotes";
    public const string BtnScanAndBackup = "Ui_BtnScanAndBackup";
    public const string BtnScanAndBackupTooltip = "Ui_BtnScanAndBackupTooltip";
    public const string BtnCheckInstalled = "Ui_BtnCheckInstalled";
    public const string BtnCheckInstalledTooltip = "Ui_BtnCheckInstalledTooltip";
    public const string BtnRetryFailed = "Ui_BtnRetryFailed";
    public const string BtnCancelInstall = "Ui_BtnCancelInstall";
    public const string BtnStartInstall = "Ui_BtnStartInstall";
    public const string TabSearchWinget = "Ui_TabSearchWinget";
    public const string SearchPlaceholder = "Ui_SearchPlaceholder";
    public const string BtnSearch = "Ui_BtnSearch";
    public const string BtnClearSearch = "Ui_BtnClearSearch";
    public const string ColumnVersion = "Ui_ColumnVersion";
    public const string ColumnSource = "Ui_ColumnSource";
    public const string BtnAddToProfile = "Ui_BtnAddToProfile";
    public const string TabInstallResults = "Ui_TabInstallResults";
    public const string ColumnResultOutcome = "Ui_ColumnResultOutcome";
    public const string ColumnResultExitCode = "Ui_ColumnResultExitCode";
    public const string ColumnResultDuration = "Ui_ColumnResultDuration";
    public const string ColumnResultMessage = "Ui_ColumnResultMessage";
    public const string TabLogs = "Ui_TabLogs";
    public const string BtnClearLogs = "Ui_BtnClearLogs";
    public const string BtnCopyAllLogs = "Ui_BtnCopyAllLogs";
    public const string BtnOpenLogFolder = "Ui_BtnOpenLogFolder";
    public const string ChkAutoScroll = "Ui_ChkAutoScroll";
    public const string ColumnLogTime = "Ui_ColumnLogTime";
    public const string ColumnLogLevel = "Ui_ColumnLogLevel";
    public const string ColumnLogMessage = "Ui_ColumnLogMessage";
    public const string ColumnLogCommand = "Ui_ColumnLogCommand";
    public const string BtnRestartAsAdmin = "Ui_BtnRestartAsAdmin";
    public const string BtnOpenDataFolder = "Ui_BtnOpenDataFolder";
    public const string BtnImportJson = "Ui_BtnImportJson";
    public const string BtnExportAll = "Ui_BtnExportAll";
    public const string BtnExportCurrent = "Ui_BtnExportCurrent";
}
