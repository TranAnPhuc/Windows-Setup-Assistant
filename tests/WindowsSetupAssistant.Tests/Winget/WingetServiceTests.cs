using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Infrastructure.Winget;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.Tests.Winget;

/// <summary>
/// Test cho WingetService - KHÔNG chạy winget thật, tiến trình được thay bằng FakeProcessRunner.
/// Trọng tâm: câu lệnh sinh ra có đúng và an toàn không, và exit code được diễn giải thế nào.
/// </summary>
public class WingetServiceTests
{
    private const string SearchOutput =
        "Name          Id                Version  Match\n" +
        "-----------------------------------------------------\n" +
        "Node.js       OpenJS.NodeJS     26.7.0   \n" +
        "Node.js 20    OpenJS.NodeJS.20  20.20.2  Tag: node.js\n";

    private static (WingetService Service, FakeProcessRunner Runner, RecordingLogger Logger) CreateService()
    {
        var runner = new FakeProcessRunner();
        var logger = new RecordingLogger();
        var service = new WingetService(runner, logger);
        return (service, runner, logger);
    }

    private static SoftwarePackage Chrome() => new()
    {
        Name = "Google Chrome",
        PackageId = "Google.Chrome",
        Category = SoftwareCategory.Browser,
        Source = "winget"
    };

    [Fact]
    public async Task InstallAsync_BuildsExactCommandRequiredByTheProject()
    {
        var (service, runner, _) = CreateService();

        await service.InstallAsync(Chrome());

        var call = runner.LastCall;

        Assert.Equal("winget", call.FileName);
        Assert.Equal("install", call.Verb);
        Assert.Equal("Google.Chrome", call.ValueOf("--id"));
        Assert.Equal("winget", call.ValueOf("--source"));
        Assert.True(call.Has("--exact"));
        Assert.True(call.Has("--silent"));
        Assert.True(call.Has("--accept-package-agreements"));
        Assert.True(call.Has("--accept-source-agreements"));
    }

    [Fact]
    public async Task InstallAsync_PassesEachArgumentSeparately_NoStringConcatenation()
    {
        // Bảo đảm không ai vô tình ghép "--id Google.Chrome" thành một chuỗi.
        var (service, runner, _) = CreateService();

        await service.InstallAsync(Chrome());

        Assert.All(runner.LastCall.Arguments, argument => Assert.DoesNotContain(' ', argument));
    }

    [Fact]
    public async Task InstallAsync_InvalidPackageId_FailsWithoutRunningWinget()
    {
        var (service, runner, _) = CreateService();

        var package = new SoftwarePackage { Name = "Độc hại", PackageId = "Google.Chrome && shutdown /s" };

        var result = await service.InstallAsync(package);

        Assert.Equal(InstallOutcome.Failed, result.Outcome);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task InstallAsync_UnknownSource_FallsBackToWinget()
    {
        var (service, runner, _) = CreateService();

        var package = Chrome();
        package.Source = "http://nguon-la.example.com";

        await service.InstallAsync(package);

        Assert.Equal("winget", runner.LastCall.ValueOf("--source"));
    }

    [Fact]
    public async Task InstallAsync_MsstoreSource_IsAllowed()
    {
        var (service, runner, _) = CreateService();

        var package = Chrome();
        package.Source = "msstore";

        await service.InstallAsync(package);

        Assert.Equal("msstore", runner.LastCall.ValueOf("--source"));
    }

    [Theory]
    [InlineData(WingetExitCodes.Success, InstallOutcome.Succeeded)]
    [InlineData(WingetExitCodes.InstallRebootRequiredToFinish, InstallOutcome.Succeeded)]
    [InlineData(WingetExitCodes.PackageAlreadyInstalled, InstallOutcome.AlreadyInstalled)]
    [InlineData(WingetExitCodes.InstallAlreadyInstalled, InstallOutcome.AlreadyInstalled)]
    [InlineData(WingetExitCodes.UpdateNotApplicable, InstallOutcome.AlreadyInstalled)]
    [InlineData(WingetExitCodes.InstallCancelledByUser, InstallOutcome.Cancelled)]
    [InlineData(WingetExitCodes.NoApplicationsFound, InstallOutcome.Failed)]
    [InlineData(WingetExitCodes.CommandRequiresAdmin, InstallOutcome.Failed)]
    [InlineData(WingetExitCodes.DownloadFailed, InstallOutcome.Failed)]
    public async Task InstallAsync_MapsExitCodeToOutcome(int exitCode, InstallOutcome expected)
    {
        var (service, runner, _) = CreateService();
        runner.Handler = (_, _) => FakeProcessRunner.WithExitCode(exitCode);

        var result = await service.InstallAsync(Chrome());

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public async Task InstallAsync_Timeout_ReportsFailureNotCrash()
    {
        var (service, runner, _) = CreateService();
        runner.Handler = (_, _) => FakeProcessRunner.Timeout();

        var result = await service.InstallAsync(Chrome());

        Assert.Equal(InstallOutcome.Failed, result.Outcome);
        Assert.Contains("không phản hồi", result.Message);
    }

    [Fact]
    public async Task UpgradeAsync_UsesUpgradeVerb()
    {
        var (service, runner, _) = CreateService();

        var result = await service.UpgradeAsync(Chrome());

        Assert.Equal("upgrade", runner.LastCall.Verb);
        Assert.Equal(InstallOutcome.Upgraded, result.Outcome);
    }

    [Fact]
    public async Task SearchAsync_ParsesResults()
    {
        var (service, runner, _) = CreateService();
        runner.Handler = (_, _) => FakeProcessRunner.Success(SearchOutput);

        var results = await service.SearchAsync("node");

        Assert.Equal(2, results.Count);
        Assert.Equal("OpenJS.NodeJS", results[0].PackageId);
        Assert.Equal("search", runner.LastCall.Verb);
        Assert.Equal("node", runner.LastCall.ValueOf("--query"));
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyInsteadOfThrowing()
    {
        var (service, runner, _) = CreateService();
        runner.Handler = (_, _) => FakeProcessRunner.WithExitCode(WingetExitCodes.NoApplicationsFound);

        var results = await service.SearchAsync("khong-ton-tai");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_Timeout_ThrowsTimeoutException()
    {
        var (service, runner, _) = CreateService();
        runner.Handler = (_, _) => FakeProcessRunner.Timeout();

        await Assert.ThrowsAsync<TimeoutException>(() => service.SearchAsync("chrome"));
    }

    [Fact]
    public async Task SearchAsync_DangerousQuery_IsRejectedBeforeRunning()
    {
        var (service, runner, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("--source malicious"));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task IsInstalledAsync_MatchesExactIdOnly()
    {
        var (service, runner, _) = CreateService();
        runner.Handler = (_, _) => FakeProcessRunner.Success(
            "Name Id      Version  Source\n" +
            "-----------------------------\n" +
            "Git  Git.Git 2.55.0.5 winget\n");

        Assert.True(await service.IsInstalledAsync("Git.Git"));

        runner.Handler = (_, _) => FakeProcessRunner.WithExitCode(
            WingetExitCodes.NoApplicationsFound,
            "No installed package found matching input criteria.");

        Assert.False(await service.IsInstalledAsync("Mozilla.Firefox"));
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsVersion()
    {
        var (service, runner, _) = CreateService();
        runner.Handler = (_, _) => FakeProcessRunner.Success("v1.29.290\n");

        var availability = await service.CheckAvailabilityAsync();

        Assert.True(availability.IsAvailable);
        Assert.Equal("v1.29.290", availability.Version);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WingetMissing_ReturnsGuidance()
    {
        var runner = new FakeProcessRunner
        {
            Handler = (_, _) => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified.")
        };

        var service = new WingetService(runner, new RecordingLogger());

        var availability = await service.CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.Contains("App Installer", availability.ErrorMessage);
    }

    [Fact]
    public async Task DisableInteractivity_OnlyAddedForWinget14OrNewer()
    {
        var (service, runner, _) = CreateService();

        // Chưa dò phiên bản: không thêm cờ (an toàn cho App Installer đời cũ).
        await service.InstallAsync(Chrome());
        Assert.False(runner.LastCall.Has("--disable-interactivity"));

        runner.Handler = (_, args) => args[0] == "--version"
            ? FakeProcessRunner.Success("v1.29.290")
            : FakeProcessRunner.Success(string.Empty);

        await service.CheckAvailabilityAsync();
        await service.InstallAsync(Chrome());

        Assert.True(runner.LastCall.Has("--disable-interactivity"));
    }

    [Fact]
    public async Task DisableInteractivity_NotAddedForOldWinget()
    {
        var (service, runner, _) = CreateService();

        runner.Handler = (_, args) => args[0] == "--version"
            ? FakeProcessRunner.Success("v1.2.10691")
            : FakeProcessRunner.Success(string.Empty);

        await service.CheckAvailabilityAsync();
        await service.InstallAsync(Chrome());

        Assert.False(runner.LastCall.Has("--disable-interactivity"));
    }

    [Theory]
    [InlineData("v1.29.290", 1, 29, 290)]
    [InlineData("1.4.0", 1, 4, 0)]
    [InlineData("v1.6", 1, 6, 0)]
    [InlineData("v1.10.40-preview", 1, 10, 40)]
    public void ParseVersion_ReadsWingetVersionText(string text, int major, int minor, int build)
    {
        var version = WingetService.ParseVersion(text);

        Assert.NotNull(version);
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-co-so")]
    public void ParseVersion_InvalidText_ReturnsNull(string? text)
    {
        Assert.Null(WingetService.ParseVersion(text));
    }

    [Fact]
    public async Task InstallAsync_UserCancellation_PropagatesOperationCanceled()
    {
        var (service, _, _) = CreateService();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.InstallAsync(Chrome(), null, cts.Token));
    }
}
