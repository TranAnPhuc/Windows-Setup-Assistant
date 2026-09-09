using WindowsSetupAssistant.App.Cli;

namespace WindowsSetupAssistant.App.Tests.Cli;

public class ConsoleMessagesTests
{
    [Fact]
    public void HuongDanNhacDuMoiThamSo()
    {
        var help = ConsoleMessages.BuildHelp();

        Assert.Contains("--unattended", help);
        Assert.Contains("--profile", help);
        Assert.Contains("--existing", help);
        Assert.Contains("--report", help);
        Assert.Contains("--help", help);
    }

    [Fact]
    public void HuongDanLietKeDuNamMaThoat()
    {
        var help = ConsoleMessages.BuildHelp();

        foreach (var code in Enum.GetValues<UnattendedExitCode>())
        {
            Assert.Contains($"  {(int)code}", help);
        }
    }

    [Fact]
    public void HuongDanCanhBaoVeQuyenAdministrator()
    {
        // Day la nguyen nhan so mot khien mot luot cai "chay xong ma khong thay phan mem dau".
        Assert.Contains("Administrator", ConsoleMessages.BuildHelp());
    }

    [Fact]
    public void DongTienTrinhCoSoThuTuVaTenGoi()
    {
        var line = ConsoleMessages.PackageStarting(3, 6, "Git.Git");

        Assert.Contains("[3/6]", line);
        Assert.Contains("Git.Git", line);
    }

    [Fact]
    public void DongKetQuaThanhCongCoThoiGian()
    {
        var line = ConsoleMessages.PackageFinished("OK", TimeSpan.FromSeconds(24.3));

        Assert.Contains("OK", line);
        Assert.Contains("24.3s", line);
    }

    [Fact]
    public void DongTongKetLietKeDuNamCon()
    {
        var line = ConsoleMessages.Totals(succeeded: 5, upgraded: 1, skipped: 2, failed: 1, cancelled: 0);

        Assert.Contains("5 succeeded", line);
        Assert.Contains("1 upgraded", line);
        Assert.Contains("2 skipped", line);
        Assert.Contains("1 failed", line);
        Assert.Contains("0 cancelled", line);
    }

    /// <summary>
    /// Moi chuoi hien thi cua che do khong giam sat. Tham so truyen vao co tinh la ASCII
    /// thuan de bat ky chu tieng Viet nao tim thay deu chac chan den tu ban than chuoi mau,
    /// khong phai tu du lieu test.
    /// </summary>
    public static IEnumerable<string> MoiChuoiHienThi() => new[]
    {
        ConsoleMessages.ModeBanner,
        ConsoleMessages.WingetMissing,
        ConsoleMessages.NothingToDo,
        ConsoleMessages.CancelRequested,
        ConsoleMessages.InvalidArguments,
        ConsoleMessages.UnexpectedError("boom"),
        ConsoleMessages.WingetDetected("v1.9.0"),
        ConsoleMessages.WingetDetected(null),
        ConsoleMessages.ProfileSummary("Work", 8, 6),
        ConsoleMessages.ProfileSummaryUnknownInstallState("Work", 8),
        ConsoleMessages.ProfileNotFound("Nope", new[] { "Work", "Home" }),
        ConsoleMessages.DataFileMissing(@"C:\app\Data\software-list.json"),
        ConsoleMessages.PackageStarting(1, 2, "Git.Git"),
        ConsoleMessages.PackageFinished("OK", TimeSpan.FromSeconds(3)),
        ConsoleMessages.PackageFailed("Git.Git", -1978335215, "installer failed"),
        ConsoleMessages.PackageFailed("Git.Git", null, "installer failed"),
        ConsoleMessages.Totals(1, 2, 3, 4, 5),
        ConsoleMessages.Elapsed(TimeSpan.FromSeconds(75)),
        ConsoleMessages.ReportWritten(@"C:\app\Reports\r.json"),
        ConsoleMessages.ReportFailed(@"C:\app\Reports\r.json", "access denied"),
        ConsoleMessages.ExitLine(UnattendedExitCode.Success),
        ConsoleMessages.ExitLine(UnattendedExitCode.SomePackagesFailed),
        ConsoleMessages.ExitLine(UnattendedExitCode.InvalidArguments),
        ConsoleMessages.ExitLine(UnattendedExitCode.WingetMissing),
        ConsoleMessages.ExitLine(UnattendedExitCode.Cancelled),
        ConsoleMessages.BuildHelp()
    };

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("/?")]
    public void HuongDanLietKeDuMoiCachGoiHelpMaParserChapNhan(string alias)
    {
        // Parser chap nhan bon dang. Neu huong dan chi liet ke mot phan thi nguoi dung
        // khong biet ba dang con lai ton tai, va nguoc lai neu huong dan quang cao mot
        // dang ma parser khong hieu thi con te hon.
        Assert.Equal(CommandLineMode.Help, CommandLineParser.Parse(new[] { alias }).Mode);
        Assert.Contains(alias, ConsoleMessages.BuildHelp());
    }

    [Fact]
    public void MoiChuoiDeuLaTiengAnh()
    {
        // Chuoi cua che do nay do MAY doc va do ky thuat vien khac doc lai sau,
        // nen khong duoc dich - xem muc 7 cua spec.
        foreach (var text in MoiChuoiHienThi())
        {
            Assert.DoesNotMatch("[àáảãạăâđêôơưÀÁẢÃẠĂÂĐÊÔƠƯ]", text);
        }
    }

    [Fact]
    public void KhongChuoiNaoLaTiengVietVietKhongDau()
    {
        // Chan ca tieng Viet viet khong dau: go "khong tim thay goi" thay vi "không tìm thấy gói"
        // van la sai, ma bo loc dau thanh o tren khong bat duoc.
        //
        // Danh sach co tinh chi gom nhung tu va cum tu KHONG THE la tieng Anh. Cac tu nhu
        // "may", "cai", "tin", "dan" bi loai ra vi trung mat chu voi tu tieng Anh that.
        const string tiengVietKhongDau =
            @"(?i)\b(khong|duoc|nguoi|thoat|quyen|huy bo|phan mem|cai dat|tien trinh|" +
            @"danh sach|thu muc|tap tin|bao cao|nhat ky|cau hinh|mac dinh|tham so|" +
            @"khoi dong|may tinh|loi xay ra)\b";

        foreach (var text in MoiChuoiHienThi())
        {
            Assert.DoesNotMatch(tiengVietKhongDau, text);
        }
    }
}
