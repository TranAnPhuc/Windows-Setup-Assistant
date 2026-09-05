using System.IO;
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Infrastructure.Persistence;

namespace WindowsSetupAssistant.App.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public Task SelectionCountsFilteringAndReorderingPersistAcrossReload() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        fixture.Winget.InstalledIds.Add("Test.Alpha");
        await fixture.InitializeAsync();
        var vm = fixture.ViewModel;
        Assert.Equal(3, vm.SelectedCount);
        Assert.Equal(1, vm.InstalledCount);
        vm.SelectNoneCommand.Execute(null);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.InstallSelectedCommand.CanExecute(null));
        vm.FilterText = "beta";
        Assert.Equal(1, vm.VisibleCount);
        vm.SelectAllCommand.Execute(null);
        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.Packages.Single(package => package.PackageId == "Test.Beta").IsSelected);
        vm.FilterText = "";
        vm.SelectNotInstalledCommand.Execute(null);
        Assert.Equal(2, vm.SelectedCount);
        Assert.False(vm.Packages[0].IsSelected);
        var beta = vm.Packages[1];
        vm.SelectedPackage = beta;
        vm.MoveUpCommand.Execute(null);
        Assert.Same(beta, vm.Packages[0]);
        Assert.False(vm.MoveUpCommand.CanExecute(null));
        Assert.True(vm.MoveDownCommand.CanExecute(null));
        Assert.True(await vm.PrepareForCloseAsync());
        var reloaded = await new JsonProfileRepository(fixture.Repository.DataFilePath).LoadAsync();
        Assert.Equal(new[] { "Test.Beta", "Test.Alpha", "Test.Gamma" }, reloaded.GetActiveProfile()!.Packages.Select(p => p.PackageId));
        Assert.Equal(new[] { 0, 1, 2 }, reloaded.GetActiveProfile()!.Packages.Select(p => p.SortOrder));
        Assert.Equal(2, reloaded.GetActiveProfile()!.Packages.Count(p => p.IsSelected));
        fixture.AssertHealthy();
    });

    [Fact]
    public Task AddEditDeleteAndProfileCommandsPersistWithoutRealDialogs() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();
        var vm = fixture.ViewModel;
        fixture.Dialogs.EditPackage = editor => { editor.Name = "Delta"; editor.PackageId = "Test.Delta"; };
        vm.AddPackageCommand.Execute(null);
        Assert.Equal(4, vm.TotalCount);
        vm.SelectedPackage = vm.Packages.Single(p => p.PackageId == "Test.Delta");
        fixture.Dialogs.EditPackage = editor => editor.Name = "Delta renamed";
        vm.EditPackageCommand.Execute(null);
        Assert.Equal("Delta renamed", vm.SelectedPackage!.Name);
        vm.DeletePackageCommand.Execute(null);
        Assert.Equal(3, vm.TotalCount);
        Assert.DoesNotContain(vm.Packages, p => p.PackageId == "Test.Delta");

        fixture.Dialogs.InputText = "New profile";
        vm.NewProfileCommand.Execute(null);
        Assert.Equal("New profile", vm.SelectedProfile!.Name);
        Assert.Empty(vm.Packages);
        fixture.Dialogs.InputText = "Renamed profile";
        vm.RenameProfileCommand.Execute(null);
        Assert.Equal("Renamed profile", vm.SelectedProfile.Name);
        vm.DuplicateProfileCommand.Execute(null);
        Assert.Equal(5, vm.Profiles.Count);
        Assert.NotEqual(vm.Profiles[3].Id, vm.SelectedProfile.Id);
        vm.DeleteProfileCommand.Execute(null);
        Assert.Equal(4, vm.Profiles.Count);
        Assert.True(await vm.PrepareForCloseAsync());
        var reloaded = await fixture.Repository.LoadAsync();
        Assert.Equal(4, reloaded.Profiles.Count);
        Assert.Contains(reloaded.Profiles, profile => profile.Name == "Renamed profile");
        Assert.Empty(fixture.Winget.InstallCalls);
        fixture.AssertHealthy();
    });

    [Fact]
    public Task ImportExportRoundTripPreservesMultipleProfilesAndSelectedProfile() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();
        var vm = fixture.ViewModel;
        vm.SelectedProfile = vm.Profiles[1];
        fixture.Dialogs.ExportPath = Path.Combine(fixture.DirectoryPath, "export-all.json");
        await WpfTestHost.ExecuteAsync(vm.ExportCommand);
        var exported = await fixture.Repository.ImportAsync(fixture.Dialogs.ExportPath);
        Assert.Equal(3, exported.Profiles.Count);
        Assert.Equal("Developer test", exported.GetActiveProfile()!.Name);

        fixture.Dialogs.ExportPath = Path.Combine(fixture.DirectoryPath, "export-profile.json");
        await WpfTestHost.ExecuteAsync(vm.ExportProfileCommand);
        var single = await fixture.Repository.ImportAsync(fixture.Dialogs.ExportPath);
        Assert.Single(single.Profiles);
        Assert.Equal("Test.Editor", Assert.Single(single.Profiles[0].Packages).PackageId);

        fixture.Dialogs.InputText = "Temporary profile";
        vm.NewProfileCommand.Execute(null);
        fixture.Dialogs.ImportPath = Path.Combine(fixture.DirectoryPath, "export-all.json");
        fixture.Dialogs.ConfirmResult = true;
        await WpfTestHost.ExecuteAsync(vm.ImportCommand);
        Assert.Equal(3, vm.Profiles.Count);
        Assert.Equal("Developer test", vm.SelectedProfile!.Name);
        fixture.Dialogs.ImportPath = Path.Combine(fixture.DirectoryPath, "export-profile.json");
        fixture.Dialogs.ConfirmResult = false;
        await WpfTestHost.ExecuteAsync(vm.ImportCommand);
        Assert.Equal(4, vm.Profiles.Count);
        Assert.Equal(4, vm.Profiles.Select(p => p.Id).Distinct().Count());
        fixture.Dialogs.ConfirmResult = true;
        Assert.True(await vm.PrepareForCloseAsync());
        Assert.Equal(4, (await fixture.Repository.LoadAsync()).Profiles.Count);
        Assert.Empty(fixture.Winget.InstallCalls);
        fixture.AssertHealthy();
    });

    [Fact]
    public Task SearchAddsFromFakeResultsAndDetectsDuplicates() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();
        var search = fixture.ViewModel.Search;
        Assert.False(search.SearchCommand.CanExecute(null));
        fixture.Winget.SearchResults = new[]
        {
            new WingetPackageInfo("Delta", "Test.Delta", "1.0", null, "winget"),
            new WingetPackageInfo("Alpha", "Test.Alpha", "1.0", null, "winget")
        };
        search.SearchText = "test";
        await WpfTestHost.ExecuteAsync(search.SearchCommand);
        Assert.Equal(new[] { "test" }, fixture.Winget.SearchCalls);
        Assert.False(search.Results[0].IsAlreadyInList);
        Assert.True(search.Results[1].IsAlreadyInList);
        search.AddCommand.Execute(search.Results[0]);
        Assert.Equal(4, fixture.ViewModel.TotalCount);
        Assert.True(search.Results[0].IsAlreadyInList);
        search.AddCommand.Execute(search.Results[0]);
        Assert.Equal(4, fixture.ViewModel.TotalCount);
        Assert.Empty(fixture.Winget.InstallCalls);
        fixture.AssertHealthy();
    });

    [Fact]
    public Task CancellingCloseLeavesQueueRunningAndConfirmedCloseCancelsAndAwaitsIt() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();
        var vm = fixture.ViewModel;
        var started = false;
        var cancellationObserved = false;
        fixture.Winget.BeforeInstall = async (_, token) =>
        {
            started = true;
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancellationObserved = token.IsCancellationRequested; }
        };
        vm.InstallSelectedCommand.Execute(null);
        await WpfTestHost.WaitUntilAsync(() => started);
        fixture.Dialogs.ConfirmResult = false;
        Assert.False(await vm.PrepareForCloseAsync());
        Assert.True(vm.IsInstalling);
        Assert.False(cancellationObserved);
        fixture.Dialogs.ConfirmResult = true;
        Assert.True(await vm.PrepareForCloseAsync());
        await WpfTestHost.DrainAsync();
        Assert.True(cancellationObserved);
        Assert.False(vm.IsInstalling);
        Assert.Equal(new[] { "Test.Alpha" }, fixture.Winget.InstallCalls);
        Assert.Equal(3, (await fixture.Repository.LoadAsync()).GetActiveProfile()!.Packages.Count);
        Assert.Empty(fixture.CommandErrors);
        Assert.Empty(fixture.Dialogs.Errors);
    });
}
