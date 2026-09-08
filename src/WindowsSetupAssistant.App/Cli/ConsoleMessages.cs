using System.Globalization;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Toàn bộ chữ của chế độ không giám sát. CỐ Ý CHỈ CÓ TIẾNG ANH và cố ý không nằm
/// trong file .resx: nội dung này do script đọc và do kỹ thuật viên khác đọc lại sau,
/// nên phải giống nhau trên mọi máy bất kể ngôn ngữ giao diện. Xem mục 7 của spec.
/// </summary>
public static class ConsoleMessages
{
    public const string ModeBanner = "Windows Setup Assistant - unattended mode";

    public const string WingetMissing =
        "WinGet was not found on this machine. Install \"App Installer\" from the Microsoft Store, then run this command again.";

    public const string NothingToDo = "Nothing to do: every selected package is already installed.";

    public const string CancelRequested = "Ctrl+C received - finishing the current package, then stopping.";

    public const string InvalidArguments = "Invalid arguments.";

    public static string UnexpectedError(string message) => $"Unexpected error: {message}";

    public static string WingetDetected(string? version) =>
        $"WinGet {version ?? "(unknown version)"} detected.";

    public static string ProfileSummary(string profileName, int total, int notInstalled) =>
        $"Profile \"{profileName}\": {total} package(s), {notInstalled} not installed.";

    // Dùng khi quét gói đã cài bị lỗi: không được bịa ra một con số "not installed" trông
    // như sự thật trong khi thực ra không biết gì cả - phải nói thẳng là "unknown".
    public static string ProfileSummaryUnknownInstallState(string profileName, int total) =>
        $"Profile \"{profileName}\": {total} package(s), install state unknown (scan failed - checking each package individually).";

    public static string ProfileNotFound(string requested, IEnumerable<string> available) =>
        $"Profile \"{requested}\" was not found. Available profiles: {string.Join(", ", available.Select(name => $"\"{name}\""))}.";

    public static string DataFileMissing(string dataFilePath) =>
        $"WARNING: \"{dataFilePath}\" does not exist, so the built-in sample list is being used instead. The packages below are probably NOT the ones you prepared - did you copy only the .exe and leave the Data folder behind?";

    public static string PackageStarting(int position, int total, string packageId) =>
        $"[{position}/{total}] {packageId} ...";

    public static string PackageFinished(string outcome, TimeSpan duration) =>
        $"    {outcome} ({duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s)";

    public static string PackageFailed(string packageId, int? exitCode, string message)
    {
        var code = exitCode is null
            ? "no exit code"
            : $"exit 0x{exitCode.Value:X8}";

        return $"    FAILED {packageId} ({code}): {message}";
    }

    public static string Totals(int succeeded, int upgraded, int skipped, int failed, int cancelled) =>
        $"{succeeded} succeeded, {upgraded} upgraded, {skipped} skipped, {failed} failed, {cancelled} cancelled.";

    public static string Elapsed(TimeSpan duration) =>
        $"Done in {(int)duration.TotalMinutes}m {duration.Seconds}s.";

    public static string ReportWritten(string path) => $"Report: {path}";

    public static string ReportFailed(string path, string reason) =>
        $"WARNING: the report could not be written to \"{path}\": {reason}";

    public static string ExitLine(UnattendedExitCode code) =>
        $"Exit code {(int)code} ({Describe(code)}).";

    private static string Describe(UnattendedExitCode code) => code switch
    {
        UnattendedExitCode.Success => "all packages handled",
        UnattendedExitCode.SomePackagesFailed => "one or more packages failed",
        UnattendedExitCode.InvalidArguments => "invalid arguments",
        UnattendedExitCode.WingetMissing => "WinGet not available",
        UnattendedExitCode.Cancelled => "cancelled by user",
        _ => "unknown"
    };

    public static string BuildHelp() => string.Join(Environment.NewLine, new[]
    {
        ModeBanner,
        "",
        "USAGE",
        "  WindowsSetupAssistant.exe --unattended [options]",
        "  WindowsSetupAssistant.exe                     (no arguments: opens the normal window)",
        "",
        "OPTIONS",
        "  --unattended            Required. Run one installation pass with no window.",
        "  --profile <name>        Profile to install. Default: the profile selected in the data file.",
        "  --existing skip|upgrade What to do with packages already on the machine. Default: skip.",
        "  --report <path>         Where to write the JSON report.",
        "                          Default: Reports\\unattended-<yyyyMMdd-HHmmss>.json next to the .exe.",
        "  --help, -h, -?          Show this help.",
        "",
        "EXIT CODES",
        "  0  All packages handled (packages skipped because they were already installed count as success).",
        "  1  Finished, but at least one package failed. Read the report.",
        "  2  Invalid arguments, or the requested profile does not exist.",
        "  3  WinGet is not available on this machine.",
        "  4  Cancelled with Ctrl+C.",
        "",
        "ADMINISTRATOR RIGHTS",
        "  This mode never shows a UAC prompt, because a prompt waiting for a click would defeat",
        "  the purpose. If packages need machine-wide installation, open PowerShell or cmd as",
        "  Administrator FIRST, then run the command from there.",
        "",
        "EXAMPLE",
        "  WindowsSetupAssistant.exe --unattended --profile \"May cong ty\" --existing upgrade",
        ""
    });
}
