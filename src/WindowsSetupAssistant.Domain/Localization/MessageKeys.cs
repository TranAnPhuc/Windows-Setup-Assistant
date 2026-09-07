namespace WindowsSetupAssistant.Domain.Localization;

/// <summary>
/// Nơi DUY NHẤT liệt kê khoá thông điệp của tầng dưới.
/// Gõ sai tên hằng là lỗi biên dịch; khoá thiếu bản dịch bị test đối chiếu ở Task 2 bắt.
/// </summary>
public static class MessageKeys
{
    // --- Kiểm tra Package Id / từ khoá tìm kiếm ---
    public const string PackageIdEmpty = "Msg_PackageIdEmpty";
    public const string PackageIdTooLong = "Msg_PackageIdTooLong";
    public const string PackageIdStartsWithDash = "Msg_PackageIdStartsWithDash";
    public const string PackageIdInvalidCharacters = "Msg_PackageIdInvalidCharacters";
    public const string SearchQueryEmpty = "Msg_SearchQueryEmpty";
    public const string SearchQueryTooLong = "Msg_SearchQueryTooLong";
    public const string SearchQueryStartsWithDash = "Msg_SearchQueryStartsWithDash";
    public const string SearchQueryControlCharacters = "Msg_SearchQueryControlCharacters";

    // --- Kết quả cài đặt ---
    public const string InstallSucceeded = "Msg_InstallSucceeded";
    public const string UpgradeSucceeded = "Msg_UpgradeSucceeded";
    public const string SkippedAlreadyInstalled = "Msg_SkippedAlreadyInstalled";
    public const string InstallFailed = "Msg_InstallFailed";
    public const string CancelledBeforeStart = "Msg_CancelledBeforeStart";
    public const string CancelledByUser = "Msg_CancelledByUser";
    public const string UnexpectedError = "Msg_UnexpectedError";
    public const string WingetNotResponding = "Msg_WingetNotResponding";
    public const string WingetExecutableMissing = "Msg_WingetExecutableMissing";

    // --- Hàng đợi cài đặt ---
    public const string QueueStarted = "Msg_QueueStarted";
    public const string QueueFinished = "Msg_QueueFinished";
    public const string QueueStoppedOnFirstError = "Msg_QueueStoppedOnFirstError";
    public const string QueueProcessing = "Msg_QueueProcessing";
    public const string QueueItemDone = "Msg_QueueItemDone";
    public const string QueueCompleted = "Msg_QueueCompleted";
    public const string QueueCancelled = "Msg_QueueCancelled";
    public const string InstalledStateCheckFailed = "Msg_InstalledStateCheckFailed";

    // --- Mã lỗi WinGet (một khoá cho mỗi nhánh của WingetExitCodes.Describe) ---
    public const string ExitSuccess = "Msg_ExitSuccess";
    public const string ExitNoApplicationsFound = "Msg_ExitNoApplicationsFound";
    public const string ExitAlreadyInstalled = "Msg_ExitAlreadyInstalled";
    public const string ExitNoUpgrade = "Msg_ExitNoUpgrade";
    public const string ExitRequiresAdmin = "Msg_ExitRequiresAdmin";
    public const string ExitAccessDenied = "Msg_ExitAccessDenied";
    public const string ExitProhibitsElevation = "Msg_ExitProhibitsElevation";
    public const string ExitDownloadFailed = "Msg_ExitDownloadFailed";
    public const string ExitZeroByteFile = "Msg_ExitZeroByteFile";
    public const string ExitNoNetwork = "Msg_ExitNoNetwork";
    public const string ExitHashMismatch = "Msg_ExitHashMismatch";
    public const string ExitNoApplicableInstaller = "Msg_ExitNoApplicableInstaller";
    public const string ExitNoSources = "Msg_ExitNoSources";
    public const string ExitSourceOpenFailed = "Msg_ExitSourceOpenFailed";
    public const string ExitServiceUnavailable = "Msg_ExitServiceUnavailable";
    public const string ExitMultipleFound = "Msg_ExitMultipleFound";
    public const string ExitInstallInProgress = "Msg_ExitInstallInProgress";
    public const string ExitPackageInUse = "Msg_ExitPackageInUse";
    public const string ExitMissingDependency = "Msg_ExitMissingDependency";
    public const string ExitDiskFull = "Msg_ExitDiskFull";
    public const string ExitInsufficientMemory = "Msg_ExitInsufficientMemory";
    public const string ExitRebootRequired = "Msg_ExitRebootRequired";
    public const string ExitCancelled = "Msg_ExitCancelled";
    public const string ExitDowngrade = "Msg_ExitDowngrade";
    public const string ExitBlockedByPolicy = "Msg_ExitBlockedByPolicy";
    public const string ExitInvalidArguments = "Msg_ExitInvalidArguments";
    public const string ExitCustomInstallerError = "Msg_ExitCustomInstallerError";
    public const string ExitInternalError = "Msg_ExitInternalError";
    public const string ExitCommandFailed = "Msg_ExitCommandFailed";
    public const string ExitUnknown = "Msg_ExitUnknown";

    // --- Dữ liệu / lưu trữ ---
    public const string SeedProfilePersonal = "Msg_SeedProfilePersonal";
    public const string SeedProfilePersonalDescription = "Msg_SeedProfilePersonalDescription";
    public const string SeedProfileDeveloper = "Msg_SeedProfileDeveloper";
    public const string SeedProfileDeveloperDescription = "Msg_SeedProfileDeveloperDescription";
    public const string SeedProfileCompany = "Msg_SeedProfileCompany";
    public const string SeedProfileCompanyDescription = "Msg_SeedProfileCompanyDescription";
    public const string SeedCatalogCreated = "Msg_SeedCatalogCreated";
    public const string CatalogFileCorrupted = "Msg_CatalogFileCorrupted";
    public const string CatalogReadFailed = "Msg_CatalogReadFailed";
    public const string CatalogExported = "Msg_CatalogExported";
    public const string CatalogImported = "Msg_CatalogImported";
    public const string ImportFileNotFound = "Msg_ImportFileNotFound";
    public const string ImportInvalidJson = "Msg_ImportInvalidJson";
    public const string ImportNoProfiles = "Msg_ImportNoProfiles";
    public const string PackageDroppedInvalidId = "Msg_PackageDroppedInvalidId";
    public const string SaveFailed = "Msg_SaveFailed";

    // --- WinGet / môi trường ---
    public const string WingetDetected = "Msg_WingetDetected";
    public const string WingetNotFound = "Msg_WingetNotFound";
    public const string WingetCheckFailed = "Msg_WingetCheckFailed";
    public const string WingetReturnedError = "Msg_WingetReturnedError";
    public const string WingetSearchFailed = "Msg_WingetSearchFailed";
    public const string WingetSearchTimeout = "Msg_WingetSearchTimeout";
    public const string InstalledListFailed = "Msg_InstalledListFailed";
    public const string InstalledListTimeout = "Msg_InstalledListTimeout";

    // --- Quét và sao lưu ---
    public const string ScanStarted = "Msg_ScanStarted";
    public const string ScanFinished = "Msg_ScanFinished";
    public const string ScanFailed = "Msg_ScanFailed";

    // --- Nhật ký chung ---
    public const string CommandSucceeded = "Msg_CommandSucceeded";
    public const string CommandFailed = "Msg_CommandFailed";
    public const string AppStarted = "Msg_AppStarted";
    public const string UnhandledError = "Msg_UnhandledError";
}
