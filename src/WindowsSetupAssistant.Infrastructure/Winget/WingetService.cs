using WindowsSetupAssistant.Domain.Localization;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.Infrastructure.Winget;

/// <summary>
/// Hiện thực <see cref="IWingetService"/> bằng cách gọi winget.exe.
///
/// Mọi tham số đều đi qua <see cref="IProcessRunner"/> dưới dạng danh sách,
/// và Package Id luôn được kiểm tra bằng <see cref="PackageIdValidator"/> trước khi chạy.
/// </summary>
public sealed partial class WingetService : IWingetService
{
    private readonly IProcessRunner _processRunner;
    private readonly IAppLogger _logger;
    private readonly WingetOptions _options;

    /// <summary>
    /// Phiên bản winget phát hiện được. Dùng để bật/tắt các tham số chỉ có ở bản mới,
    /// tránh lỗi "Invalid command line arguments" trên máy Windows 10 có App Installer cũ.
    /// </summary>
    private Version? _detectedVersion;

    public WingetService(IProcessRunner processRunner, IAppLogger logger, WingetOptions? options = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new WingetOptions();
    }

    [GeneratedRegex(@"(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    /// <summary>--disable-interactivity chỉ có từ WinGet 1.4 trở lên.</summary>
    private bool SupportsDisableInteractivity =>
        _detectedVersion is not null && _detectedVersion >= new Version(1, 4);

    public async Task<WingetAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processRunner
                .RunAsync(_options.ExecutableName, new[] { "--version" }, _options.VersionTimeout, null, cancellationToken)
                .ConfigureAwait(false);

            if (result.TimedOut)
            {
                return WingetAvailability.NotAvailable("WinGet không phản hồi. Hãy thử mở Terminal và chạy lệnh 'winget --version'.");
            }

            if (result.ExitCode != WingetExitCodes.Success)
            {
                return WingetAvailability.NotAvailable(
                    $"Chạy được winget nhưng trả về lỗi: {WingetExitCodes.Describe(result.ExitCode)}");
            }

            var versionText = result.StandardOutput.Trim();
            _detectedVersion = ParseVersion(versionText);
            _logger.Information($"Đã phát hiện WinGet {versionText}.", result.Command);

            return WingetAvailability.Available(versionText);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Win32Exception)
        {
            // Không tìm thấy winget.exe trong PATH.
            return WingetAvailability.NotAvailable(
                "Không tìm thấy WinGet trên máy này. Hãy cài 'App Installer' từ Microsoft Store " +
                "(hoặc tải bản .msixbundle chính thức từ GitHub: microsoft/winget-cli/releases) rồi mở lại ứng dụng.");
        }
        catch (Exception ex)
        {
            return WingetAvailability.NotAvailable($"Không kiểm tra được WinGet: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<WingetPackageInfo>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var safeQuery = SearchQueryValidator.EnsureValid(query);

        var arguments = new List<string>
        {
            "search",
            "--query", safeQuery,
            "--source", "winget",
            "--accept-source-agreements"
        };

        AddDisableInteractivity(arguments);

        var result = await _processRunner
            .RunAsync(_options.ExecutableName, arguments, _options.SearchTimeout, null, cancellationToken)
            .ConfigureAwait(false);

        if (result.TimedOut)
        {
            throw new TimeoutException("WinGet không phản hồi khi tìm kiếm. Kiểm tra kết nối mạng rồi thử lại.");
        }

        // "Không tìm thấy gói nào" không phải lỗi - trả về danh sách rỗng.
        if (result.ExitCode == WingetExitCodes.NoApplicationsFound)
        {
            return Array.Empty<WingetPackageInfo>();
        }

        if (result.ExitCode != WingetExitCodes.Success)
        {
            _logger.Error("Tìm kiếm WinGet thất bại.", result.Command, result.ExitCode,
                WingetOutputParser.StripProgressNoise(result.CombinedOutput));

            throw new InvalidOperationException(WingetExitCodes.Describe(result.ExitCode).ToString());
        }

        var packages = WingetOutputParser.ParseTable(result.StandardOutput);

        return packages.Count > _options.MaxSearchResults
            ? packages.Take(_options.MaxSearchResults).ToList()
            : packages;
    }

    public async Task<IReadOnlyList<WingetPackageInfo>> GetInstalledPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "list", "--accept-source-agreements" };
        AddDisableInteractivity(arguments);

        var result = await _processRunner
            .RunAsync(_options.ExecutableName, arguments, _options.ListTimeout, null, cancellationToken)
            .ConfigureAwait(false);

        if (result.TimedOut)
        {
            _logger.Warning("WinGet không phản hồi khi liệt kê phần mềm đã cài.", result.Command);
            return Array.Empty<WingetPackageInfo>();
        }

        if (result.ExitCode is not WingetExitCodes.Success and not WingetExitCodes.NoApplicationsFound)
        {
            _logger.Warning($"Không lấy được danh sách phần mềm đã cài: {WingetExitCodes.Describe(result.ExitCode)}",
                result.Command);

            return Array.Empty<WingetPackageInfo>();
        }

        return WingetOutputParser.ParseTable(result.StandardOutput);
    }

    public async Task<bool> IsInstalledAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var safeId = PackageIdValidator.EnsureValid(packageId);

        var arguments = new List<string>
        {
            "list",
            "--id", safeId,
            "--exact",
            "--accept-source-agreements"
        };

        AddDisableInteractivity(arguments);

        var result = await _processRunner
            .RunAsync(_options.ExecutableName, arguments, _options.SearchTimeout, null, cancellationToken)
            .ConfigureAwait(false);

        if (result.TimedOut || result.ExitCode == WingetExitCodes.NoApplicationsFound)
        {
            return false;
        }

        return WingetOutputParser.ContainsPackageId(result.StandardOutput, safeId);
    }

    public Task<InstallationResult> InstallAsync(
        SoftwarePackage package,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default) =>
        ExecutePackageCommandAsync(package, "install", outputProgress, cancellationToken);

    public Task<InstallationResult> UpgradeAsync(
        SoftwarePackage package,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default) =>
        ExecutePackageCommandAsync(package, "upgrade", outputProgress, cancellationToken);

    private async Task<InstallationResult> ExecutePackageCommandAsync(
        SoftwarePackage package,
        string verb,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        var startedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();

        // Bước bắt buộc: xác thực Id trước khi thực thi.
        string safeId;
        try
        {
            safeId = PackageIdValidator.EnsureValid(package.PackageId);
        }
        catch (LocalizedException ex)
        {
            return new InstallationResult
            {
                PackageId = package.PackageId,
                DisplayName = DisplayNameOf(package),
                Outcome = InstallOutcome.Failed,
                Message = ex.LocalizedMessage,
                StartedAt = startedAt,
                Duration = stopwatch.Elapsed
            };
        }

        var arguments = new List<string>
        {
            verb,
            "--id", safeId,
            "--exact",
            "--source", ResolveSource(package.Source),
            "--silent",
            "--accept-package-agreements",
            "--accept-source-agreements"
        };

        AddDisableInteractivity(arguments);

        ProcessRunResult result;

        try
        {
            result = await _processRunner
                .RunAsync(_options.ExecutableName, arguments, _options.InstallTimeout, outputProgress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Win32Exception)
        {
            stopwatch.Stop();
            return new InstallationResult
            {
                PackageId = safeId,
                DisplayName = DisplayNameOf(package),
                Outcome = InstallOutcome.Failed,
                Message = LocalizedText.Of(MessageKeys.WingetExecutableMissing),
                Command = ProcessRunner.FormatCommand(_options.ExecutableName, arguments),
                StartedAt = startedAt,
                Duration = stopwatch.Elapsed
            };
        }

        stopwatch.Stop();

        var details = WingetOutputParser.StripProgressNoise(result.CombinedOutput);
        var isUpgrade = verb == "upgrade";

        if (result.TimedOut)
        {
            return Build(InstallOutcome.Failed,
                LocalizedText.Of(MessageKeys.WingetNotResponding, (int)_options.InstallTimeout.TotalMinutes));
        }

        var exitCode = result.ExitCode;

        if (exitCode == WingetExitCodes.Success)
        {
            return Build(isUpgrade ? InstallOutcome.Upgraded : InstallOutcome.Succeeded,
                LocalizedText.Of(isUpgrade ? MessageKeys.UpgradeSucceeded : MessageKeys.InstallSucceeded));
        }

        if (WingetExitCodes.RequiresReboot(exitCode))
        {
            return Build(isUpgrade ? InstallOutcome.Upgraded : InstallOutcome.Succeeded,
                WingetExitCodes.Describe(exitCode));
        }

        if (WingetExitCodes.IsAlreadyInstalled(exitCode) || WingetExitCodes.IsNoUpgradeAvailable(exitCode))
        {
            return Build(InstallOutcome.AlreadyInstalled, WingetExitCodes.Describe(exitCode));
        }

        if (WingetExitCodes.IsCancelled(exitCode))
        {
            return Build(InstallOutcome.Cancelled, WingetExitCodes.Describe(exitCode));
        }

        return Build(InstallOutcome.Failed, WingetExitCodes.Describe(exitCode));

        InstallationResult Build(InstallOutcome outcome, LocalizedText message) => new()
        {
            PackageId = safeId,
            DisplayName = DisplayNameOf(package),
            Outcome = outcome,
            ExitCode = result.TimedOut ? null : result.ExitCode,
            Command = result.Command,
            Message = message,
            StartedAt = startedAt,
            Duration = stopwatch.Elapsed,
        };
    }

    /// <summary>Chỉ chấp nhận nguồn nằm trong danh sách trắng, còn lại ép về "winget".</summary>
    private string ResolveSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "winget";
        }

        var trimmed = source.Trim();

        return _options.AllowedSources.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase))
            ? trimmed.ToLowerInvariant()
            : "winget";
    }

    private void AddDisableInteractivity(List<string> arguments)
    {
        if (SupportsDisableInteractivity)
        {
            arguments.Add("--disable-interactivity");
        }
    }

    private static string DisplayNameOf(SoftwarePackage package) =>
        string.IsNullOrWhiteSpace(package.Name) ? package.PackageId : package.Name;

    /// <summary>Đọc "v1.9.25200" -> Version(1, 9, 25200).</summary>
    public static Version? ParseVersion(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        var match = VersionPattern().Match(versionText);
        if (!match.Success)
        {
            return null;
        }

        var major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var build = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : 0;

        return new Version(major, minor, build);
    }
}
