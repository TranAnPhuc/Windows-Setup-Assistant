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

    [Fact]
    public void MoiChuoiDeuLaTiengAnh()
    {
        // Chuoi cua che do nay do MAY doc va do ky thuat vien khac doc lai sau,
        // nen khong duoc dich - xem muc 7 cua spec.
        var texts = new[]
        {
            ConsoleMessages.BuildHelp(),
            ConsoleMessages.PackageStarting(1, 1, "X"),
            ConsoleMessages.Totals(0, 0, 0, 0, 0)
        };

        foreach (var text in texts)
        {
            Assert.DoesNotMatch("[àáảãạăâđêôơưÀÁẢÃẠĂÂĐÊÔƠƯ]", text);
        }
    }
}
