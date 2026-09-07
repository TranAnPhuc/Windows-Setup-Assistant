using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.App.Services;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.Tests.Fakes;

/// <summary>Only in-memory fake calls. Never creates a process or executes WinGet.</summary>
internal sealed class UiWingetFake : IWingetService
{
    public WingetAvailability Availability { get; set; } = WingetAvailability.Available("test-only");
    public HashSet<string> InstalledIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> InstallCalls { get; } = new();
    public List<string> SearchCalls { get; } = new();
    public IReadOnlyList<WingetPackageInfo> SearchResults { get; set; } = Array.Empty<WingetPackageInfo>();
    public Func<SoftwarePackage, CancellationToken, Task>? BeforeInstall { get; set; }

    public Task<WingetAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Availability);

    public Task<IReadOnlyList<WingetPackageInfo>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SearchCalls.Add(query);
        return Task.FromResult(SearchResults);
    }

    public Task<IReadOnlyList<WingetPackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WingetPackageInfo>>(
            InstalledIds.Select(id => new WingetPackageInfo(id, id, "1.0", null, "winget")).ToArray());

    public Task<bool> IsInstalledAsync(string packageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(InstalledIds.Contains(packageId));

    public async Task<InstallationResult> InstallAsync(SoftwarePackage package,
        IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InstallCalls.Add(package.PackageId);
        if (BeforeInstall is not null)
            await BeforeInstall(package, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        InstalledIds.Add(package.PackageId);
        return new InstallationResult
        {
            PackageId = package.PackageId,
            DisplayName = package.Name,
            Outcome = InstallOutcome.Succeeded,
            ExitCode = 0,
            Message = LocalizedText.Raw("Fake only: no software installed.")
        };
    }

    public Task<InstallationResult> UpgradeAsync(SoftwarePackage package,
        IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default) =>
        InstallAsync(package, outputProgress, cancellationToken);
}

/// <summary>
/// Repository giả mà LoadAsync chỉ hoàn tất khi test cho phép.
/// Dùng để dựng đúng tình huống "đóng cửa sổ lúc danh sách chưa nạp xong",
/// khi đó MainViewModel.PrepareForCloseAsync chạy hoàn toàn đồng bộ.
/// </summary>
internal sealed class GatedProfileRepository : IProfileRepository
{
    private readonly TaskCompletionSource<SoftwareCatalog> _load =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string DataFilePath => "gated-repository (test only)";

    public int SaveCount { get; private set; }

    public async Task<SoftwareCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var registration = cancellationToken.Register(() => _load.TrySetCanceled(cancellationToken));
        return await _load.Task.ConfigureAwait(false);
    }

    /// <summary>Cho phép LoadAsync kết thúc - gọi ở cuối test để không bỏ lại tác vụ treo.</summary>
    public void CompleteLoad() => _load.TrySetResult(new SoftwareCatalog());

    public Task SaveAsync(SoftwareCatalog catalog, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task ExportAsync(SoftwareCatalog catalog, string filePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<SoftwareCatalog> ImportAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SoftwareCatalog());
}

internal sealed class UiDialogFake : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public bool InstallConfirmationResult { get; set; } = true;
    public bool AcceptEditor { get; set; } = true;
    public Action<PackageEditorViewModel>? EditPackage { get; set; }
    public string? InputText { get; set; }
    public string? ImportPath { get; set; }
    public string? ExportPath { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Messages { get; } = new();

    public void ShowInfo(string title, string message) => Messages.Add(title + ": " + message);
    public void ShowError(string title, string message) => Errors.Add(title + ": " + message);
    public bool Confirm(string title, string message) => ConfirmResult;
    public string? OpenJsonFile(string title) => ImportPath;
    public string? SaveJsonFile(string title, string suggestedFileName) => ExportPath;
    public bool ShowInstallConfirmation(InstallConfirmViewModel viewModel) => InstallConfirmationResult;
    public string? ShowTextInput(TextInputViewModel viewModel) => InputText;

    public bool ShowPackageEditor(PackageEditorViewModel viewModel)
    {
        EditPackage?.Invoke(viewModel);
        return AcceptEditor && viewModel.Validate();
    }
}
