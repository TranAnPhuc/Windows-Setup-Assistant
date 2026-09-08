using System.Globalization;
using System.IO;
using System.Text.Json;
using WindowsSetupAssistant.App.Cli;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Infrastructure.Winget;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.App.Tests.Cli;

public class UnattendedReportTests
{
    private static InstallationResult Result(
        string packageId,
        InstallOutcome outcome,
        int? exitCode = 0,
        double seconds = 1.0) => new()
    {
        PackageId = packageId,
        DisplayName = packageId,
        Outcome = outcome,
        ExitCode = exitCode,
        Message = LocalizedText.Raw($"message for {packageId}"),
        Duration = TimeSpan.FromSeconds(seconds)
    };

    private static UnattendedReport Build(InstallationRunSummary summary, UnattendedExitCode code) =>
        UnattendedReport.Create(
            summary,
            profileName: "May cong ty",
            wingetVersion: "v1.9.25200",
            existingPackageAction: ExistingPackageAction.Skip,
            exitCode: code,
            startedAt: DateTimeOffset.Parse("2026-09-08T14:10:41+07:00", CultureInfo.InvariantCulture),
            localizer: new StubLocalizer());

    [Fact]
    public void DemDungTungLoaiKetQua()
    {
        var summary = new InstallationRunSummary
        {
            Results = new[]
            {
                Result("A", InstallOutcome.Succeeded),
                Result("B", InstallOutcome.Upgraded),
                Result("C", InstallOutcome.AlreadyInstalled, exitCode: null),
                Result("D", InstallOutcome.Failed, exitCode: WingetExitCodes.NoApplicableInstaller),
                Result("E", InstallOutcome.Cancelled, exitCode: null)
            },
            TotalDuration = TimeSpan.FromSeconds(112.4)
        };

        var report = Build(summary, UnattendedExitCode.SomePackagesFailed);

        Assert.Equal(5, report.Counts.Total);
        Assert.Equal(1, report.Counts.Succeeded);
        Assert.Equal(1, report.Counts.Upgraded);
        Assert.Equal(1, report.Counts.Skipped);
        Assert.Equal(1, report.Counts.Failed);
        Assert.Equal(1, report.Counts.Cancelled);
    }

    [Fact]
    public void GoiBaoCanKhoiDongLaiDuocDanhDau()
    {
        var summary = new InstallationRunSummary
        {
            Results = new[]
            {
                Result("A", InstallOutcome.Succeeded, exitCode: WingetExitCodes.InstallRebootRequiredToFinish),
                Result("B", InstallOutcome.Succeeded, exitCode: 0)
            },
            TotalDuration = TimeSpan.FromSeconds(3)
        };

        var report = Build(summary, UnattendedExitCode.Success);

        Assert.True(report.Packages[0].RestartRequired);
        Assert.False(report.Packages[1].RestartRequired);
    }

    [Fact]
    public void TenTruongJsonDungHopDongVoiScriptBenNgoai()
    {
        var summary = new InstallationRunSummary
        {
            Results = new[] { Result("Google.Chrome", InstallOutcome.Succeeded, seconds: 24.3) },
            TotalDuration = TimeSpan.FromSeconds(24.3)
        };

        var json = UnattendedReportWriter.Serialize(Build(summary, UnattendedExitCode.Success));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("May cong ty", root.GetProperty("profileName").GetString());
        Assert.Equal("v1.9.25200", root.GetProperty("wingetVersion").GetString());
        Assert.Equal("Skip", root.GetProperty("existingPackageAction").GetString());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.False(root.GetProperty("wasCancelled").GetBoolean());
        Assert.True(root.TryGetProperty("startedAt", out _));
        Assert.True(root.TryGetProperty("finishedAt", out _));
        Assert.True(root.TryGetProperty("durationSeconds", out _));
        Assert.True(root.TryGetProperty("machineName", out _));

        var package = root.GetProperty("packages")[0];
        Assert.Equal("Google.Chrome", package.GetProperty("packageId").GetString());
        Assert.Equal("Succeeded", package.GetProperty("outcome").GetString());
        Assert.True(package.TryGetProperty("name", out _));
        Assert.True(package.TryGetProperty("exitCode", out _));
        Assert.True(package.TryGetProperty("durationSeconds", out _));
        Assert.True(package.TryGetProperty("restartRequired", out _));
        Assert.True(package.TryGetProperty("errorMessage", out _));
    }

    [Fact]
    public void GhiDuocRaFileThatVaDocLaiDuoc()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wsa-report-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "sub", "report.json");

        try
        {
            var summary = new InstallationRunSummary
            {
                Results = new[] { Result("A", InstallOutcome.Succeeded) },
                TotalDuration = TimeSpan.FromSeconds(1)
            };

            var ok = UnattendedReportWriter.TryWrite(Build(summary, UnattendedExitCode.Success), path, out var error);

            Assert.True(ok);
            Assert.Null(error);
            Assert.True(File.Exists(path));           // thu muc con phai duoc tao tu dong
            Assert.Contains("schemaVersion", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DuongDanKhongHopLeThiBaoThatBaiChuKhongNemLoi()
    {
        var summary = new InstallationRunSummary
        {
            Results = Array.Empty<InstallationResult>(),
            TotalDuration = TimeSpan.Zero
        };

        var ok = UnattendedReportWriter.TryWrite(
            Build(summary, UnattendedExitCode.Success),
            @"Z:\khong-ton-tai\a<b>c.json",
            out var error);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void DuongDanMacDinhNamTrongThuMucReports()
    {
        var path = UnattendedReportWriter.DefaultPath(
            DateTimeOffset.Parse("2026-09-08T14:12:33+07:00", CultureInfo.InvariantCulture));

        Assert.Contains("Reports", path);
        Assert.EndsWith("unattended-20260908-141233.json", path);
        Assert.True(Path.IsPathRooted(path));
    }
}
