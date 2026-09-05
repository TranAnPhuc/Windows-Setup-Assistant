using System.IO;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.App.Services;
using WindowsSetupAssistant.App.Tests.Fakes;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Infrastructure.Persistence;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.App.Tests;

internal sealed class ViewModelFixture : IAsyncDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "wsa-ui-tests-" + Guid.NewGuid().ToString("N"));
    public UiWingetFake Winget { get; } = new();
    public UiDialogFake Dialogs { get; } = new();
    public RecordingLogger Logger { get; } = new();
    public List<Exception> CommandErrors { get; } = new();
    public JsonProfileRepository Repository { get; }
    public MainViewModel ViewModel { get; }

    public ViewModelFixture()
    {
        Directory.CreateDirectory(DirectoryPath);
        Repository = new JsonProfileRepository(Path.Combine(DirectoryPath, "Data", "software-list.json"), Logger);
        ViewModel = new MainViewModel(Repository, Winget, new InstallationQueueService(Winget, Logger),
            Logger, Dialogs, new ThemeManager(), new SettingsStore(Path.Combine(DirectoryPath, "Data", "settings.json")),
            new AppSettings { CheckInstalledOnStartup = true }, new LogViewModel(Logger, Dialogs, null));
        AsyncRelayCommand.UnhandledExceptionHandler = CommandErrors.Add;
    }

    public async Task SeedAsync()
    {
        var personal = new InstallationProfile
        {
            Name = "Personal test",
            Packages = new List<SoftwarePackage>
            {
                Package("Alpha", "Test.Alpha", 0),
                Package("Beta", "Test.Beta", 1),
                Package("Gamma", "Test.Gamma", 2)
            }
        };
        await Repository.SaveAsync(new SoftwareCatalog
        {
            ActiveProfileId = personal.Id,
            Profiles = new List<InstallationProfile>
            {
                personal,
                new() { Name = "Developer test", Packages = new List<SoftwarePackage> { Package("Editor", "Test.Editor", 0) } },
                new() { Name = "Company test" }
            }
        });
    }

    public async Task InitializeAsync()
    {
        await SeedAsync();
        await WpfTestHost.ExecuteAsync(ViewModel.LoadedCommand);
        AssertHealthy();
    }

    public static SoftwarePackage Package(string name, string packageId, int order) => new()
    {
        Name = name, PackageId = packageId, SortOrder = order,
        IsSelected = true, Category = SoftwareCategory.Other, Source = "winget"
    };

    public void AssertHealthy()
    {
        Assert.Empty(CommandErrors);
        Assert.Empty(Dialogs.Errors);
        Assert.Empty(Logger.Errors);
    }

    public async ValueTask DisposeAsync()
    {
        Dialogs.ConfirmResult = true;
        await ViewModel.PrepareForCloseAsync();
        ViewModel.Dispose();
        AsyncRelayCommand.UnhandledExceptionHandler = null;
        // The exact unique fixture directory is the only filesystem cleanup target.
        Directory.Delete(DirectoryPath, recursive: true);
    }
}
