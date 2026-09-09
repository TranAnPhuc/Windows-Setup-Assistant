using System.IO;
using System.Text.Json;
using WindowsSetupAssistant.App.Cli;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Infrastructure.Persistence;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.App.Tests.Cli;

public class UnattendedRunnerTests : IDisposable
{
    private readonly string _directory;
    private readonly string _dataFile;
    private readonly string _reportFile;
    private readonly FakeWingetService _winget = new();
    private readonly RecordingLogger _logger = new();
    private readonly CapturingOutput _output = new();

    public UnattendedRunnerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "wsa-unattended-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _dataFile = Path.Combine(_directory, "software-list.json");
        _reportFile = Path.Combine(_directory, "report.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Ghi lai chu in ra de test kiem chung noi dung ma khong can console that.</summary>
    private sealed class CapturingOutput : IUnattendedOutput
    {
        public List<string> Lines { get; } = new();
        public List<string> Errors { get; } = new();
        public void WriteLine(string text = "") => Lines.Add(text);
        public void WriteError(string text) => Errors.Add(text);
        public string All => string.Join("\n", Lines.Concat(Errors));
    }

    private async Task<JsonProfileRepository> GivenCatalogAsync(params InstallationProfile[] profiles)
    {
        var repository = new JsonProfileRepository(_dataFile, _logger, new StubLocalizer());
        var catalog = new SoftwareCatalog { Profiles = profiles.ToList() };
        catalog.ActiveProfileId = profiles[0].Id;
        await repository.SaveAsync(catalog);
        return repository;
    }

    private static InstallationProfile Profile(string name, params string[] packageIds) => new()
    {
        Name = name,
        Packages = packageIds.Select((id, index) => new SoftwarePackage
        {
            Name = id,
            PackageId = id,
            SortOrder = index,
            IsSelected = true
        }).ToList()
    };

    private UnattendedRunner CreateRunner(JsonProfileRepository repository) => new(
        _winget,
        repository,
        new InstallationQueueService(_winget, _logger),
        new StubLocalizer(),
        _output,
        _logger);

    private CommandLineOptions Options(
        string? profileName = null,
        ExistingPackageAction existing = ExistingPackageAction.Skip) => new()
    {
        ProfileName = profileName,
        ExistingPackageAction = existing,
        ReportPath = _reportFile
    };

    [Fact]
    public async Task MoiGoiThanhCongThiTraVeMaKhong()
    {
        var repository = await GivenCatalogAsync(Profile("May ca nhan", "A.A", "B.B"));

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);
        Assert.Equal(new[] { "A.A", "B.B" }, _winget.InstallCalls);
    }

    [Fact]
    public async Task MotGoiLoiThiTraVeMotVaCacGoiSauVanChay()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A", "B.B", "C.C"));
        _winget.Outcomes["B.B"] = InstallOutcome.Failed;

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.SomePackagesFailed, code);
        Assert.Contains("C.C", _winget.InstallCalls);   // mot goi loi KHONG lam dung hang doi
    }

    [Fact]
    public async Task ThieuWingetThiTraVeBaVaKhongCaiGiCa()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A"));
        _winget.Availability = WingetAvailability.NotAvailable("winget not found");

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.WingetMissing, code);
        Assert.Empty(_winget.InstallCalls);
        Assert.Contains("App Installer", _output.All);
    }

    [Fact]
    public async Task SaiTenCauHinhThiTraVeHaiVaLietKeCacTenDangCo()
    {
        var repository = await GivenCatalogAsync(Profile("May ca nhan", "A.A"), Profile("May cong ty", "B.B"));

        var code = await CreateRunner(repository).RunAsync(Options(profileName: "May ke toan"), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.InvalidArguments, code);
        Assert.Contains("May ca nhan", _output.All);
        Assert.Contains("May cong ty", _output.All);
        Assert.Empty(_winget.InstallCalls);
    }

    [Fact]
    public async Task TenCauHinhKhongPhanBietHoaThuongVaKhoangTrangThua()
    {
        var repository = await GivenCatalogAsync(Profile("May ca nhan", "A.A"), Profile("May cong ty", "B.B"));

        var code = await CreateRunner(repository).RunAsync(Options(profileName: "  MAY CONG TY "), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);
        Assert.Equal(new[] { "B.B" }, _winget.InstallCalls);
    }

    [Fact]
    public async Task KhongCoThamSoProfileThiDungCauHinhDangChon()
    {
        var first = Profile("May ca nhan", "A.A");
        var second = Profile("May cong ty", "B.B");
        var repository = new JsonProfileRepository(_dataFile, _logger, new StubLocalizer());
        await repository.SaveAsync(new SoftwareCatalog
        {
            Profiles = new List<InstallationProfile> { first, second },
            ActiveProfileId = second.Id
        });

        await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(new[] { "B.B" }, _winget.InstallCalls);
    }

    [Fact]
    public async Task GoiKhongTickThiKhongDuocCai()
    {
        var profile = Profile("P", "A.A", "B.B");
        profile.Packages[1].IsSelected = false;
        var repository = await GivenCatalogAsync(profile);

        await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(new[] { "A.A" }, _winget.InstallCalls);
    }

    [Fact]
    public async Task MacDinhSkipThiGoiDaCaiKhongBiCaiLai()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A", "B.B"));
        _winget.InstalledPackageIds.Add("A.A");

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);   // bo qua van la thanh cong
        Assert.Equal(new[] { "B.B" }, _winget.InstallCalls);
        Assert.Empty(_winget.UpgradeCalls);
    }

    [Fact]
    public async Task UpgradeThiGoiDaCaiDuocNangCap()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A", "B.B"));
        _winget.InstalledPackageIds.Add("A.A");

        var code = await CreateRunner(repository).RunAsync(
            Options(existing: ExistingPackageAction.Upgrade), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);
        Assert.Equal(new[] { "A.A" }, _winget.UpgradeCalls);
        Assert.Equal(new[] { "B.B" }, _winget.InstallCalls);
    }

    [Fact]
    public async Task KhongCoGiDeLamVanLaThanhCongVaVanGhiBaoCao()
    {
        var profile = Profile("P", "A.A");
        profile.Packages[0].IsSelected = false;
        var repository = await GivenCatalogAsync(profile);

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);
        Assert.True(File.Exists(_reportFile));
        Assert.Contains("Nothing to do", _output.All);
    }

    [Fact]
    public async Task KhongCoGoiNaoDuocTickThiBoHanLuotQuetGoiDaCai()
    {
        // "winget list" mat vai giay. Khi hang doi rong thi ket qua chac chan khong
        // dung toi, nen khong duoc hoi lam gi.
        var profile = Profile("P", "A.A");
        profile.Packages[0].IsSelected = false;
        var repository = await GivenCatalogAsync(profile);

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);
        Assert.Equal(0, _winget.GetInstalledCallCount);
    }

    [Fact]
    public async Task CoGoiDuocTickThiVanPhaiQuetGoiDaCai()
    {
        // Doi chung cho test tren: chi duoc bo qua luot quet khi hang doi rong.
        var repository = await GivenCatalogAsync(Profile("P", "A.A"));

        await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(1, _winget.GetInstalledCallCount);
    }

    [Fact]
    public async Task HuyGiuaChungThiTraVeBonVaVanGhiBaoCao()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A", "B.B", "C.C"));
        using var cts = new CancellationTokenSource();
        _winget.OnInstalling = package =>
        {
            if (package.PackageId == "B.B")
            {
                cts.Cancel();
            }
        };

        var code = await CreateRunner(repository).RunAsync(Options(), cts.Token);

        Assert.Equal(UnattendedExitCode.Cancelled, code);
        Assert.True(File.Exists(_reportFile));
    }

    [Fact]
    public async Task BaoCaoDuocGhiCaKhiCoGoiLoi()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A"));
        _winget.Outcomes["A.A"] = InstallOutcome.Failed;

        await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        using var document = JsonDocument.Parse(File.ReadAllText(_reportFile));
        Assert.Equal(1, document.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("counts").GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task GhiBaoCaoThatBaiKhongLamDoiMaThoat()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A"));
        var options = new CommandLineOptions { ReportPath = @"Z:\khong-ton-tai\a<b>.json" };

        var code = await CreateRunner(repository).RunAsync(options, CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);      // cai xong van la cai xong
        Assert.Contains("WARNING", _output.All);
    }

    [Fact]
    public async Task ThieuFileDuLieuThiCanhBaoToRang()
    {
        // Tinh huong that: chep moi file .exe sang may moi, quen mat thu muc Data.
        // Repository am tham tra ve danh sach mau - khong ai ngoi truoc may de nhan ra.
        var repository = new JsonProfileRepository(_dataFile, _logger, new StubLocalizer());

        await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Contains("WARNING", _output.All);
        Assert.Contains(_dataFile, _output.All);
    }

    [Fact]
    public async Task TienTrinhTungGoiDuocInRa()
    {
        var repository = await GivenCatalogAsync(Profile("P", "A.A", "B.B"));

        await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Contains(_output.Lines, line => line.Contains("[1/2]") && line.Contains("A.A"));
        Assert.Contains(_output.Lines, line => line.Contains("[2/2]") && line.Contains("B.B"));
    }

    [Fact]
    public async Task QuetGoiDaCaiLoiThiHangDoiTuHoiTungGoi()
    {
        // Khi liet ke goi da cai bi loi, runner phai truyen null (khong biet) cho hang doi,
        // KHONG duoc gia vo la "biet chac rong". Hang doi chi hoi tung goi qua IsInstalledAsync
        // khi PreCheckedInstalledPackageIds la null.
        var repository = await GivenCatalogAsync(Profile("P", "A.A", "B.B"));
        _winget.ThrowOnGetInstalled = new InvalidOperationException("winget list failed");

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);
        Assert.Equal(new[] { "A.A", "B.B" }, _winget.IsInstalledCalls);
        Assert.Contains("install state unknown", _output.All);
    }

    [Fact]
    public async Task QuetGoiDaCaiLoiNhungGoiDaCoVanDuocBoQuaKhiSkip()
    {
        // Du khong liet ke duoc, hang doi van tu hoi tung goi va van phai bo qua dung
        // goi da cai san - khong duoc cai lai no chi vi lan quet tong the bi loi.
        var repository = await GivenCatalogAsync(Profile("P", "A.A", "B.B"));
        _winget.ThrowOnGetInstalled = new InvalidOperationException("winget list failed");
        _winget.InstalledPackageIds.Add("A.A");

        var code = await CreateRunner(repository).RunAsync(Options(), CancellationToken.None);

        Assert.Equal(UnattendedExitCode.Success, code);
        Assert.Equal(new[] { "A.A", "B.B" }, _winget.IsInstalledCalls);
        Assert.Equal(new[] { "B.B" }, _winget.InstallCalls);
    }
}
