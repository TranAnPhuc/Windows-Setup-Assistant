# Kế hoạch triển khai — Chế độ không giám sát

> **Dành cho người thực thi:** SKILL BẮT BUỘC — dùng `superpowers:subagent-driven-development`
> (khuyên dùng) hoặc `superpowers:executing-plans` để làm lần lượt từng task.
> Các bước dùng cú pháp checkbox (`- [ ]`) để đánh dấu tiến độ.

**Spec:** [2026-09-08-che-do-khong-giam-sat-design.md](../specs/2026-09-08-che-do-khong-giam-sat-design.md)

**Mục tiêu:** Chạy trọn một lượt cài đặt bằng đúng một dòng lệnh, không mở giao diện,
in tiến trình ra cửa sổ lệnh đã gọi, trả về mã thoát chi tiết và một file báo cáo JSON.

**Kiến trúc:** Vẫn một file `.exe` duy nhất. `App.OnStartup` phân nhánh theo tham số dòng
lệnh: không có `--unattended` thì dựng `MainWindow` y như cũ; có thì chạy `UnattendedRunner`
và không tạo cửa sổ nào. Nhánh mới **tái sử dụng** `WingetService`, `JsonProfileRepository`
và `InstallationQueueService` đang dùng cho giao diện, nên hành vi cài đặt của hai chế độ
không thể lệch nhau.

**Công nghệ:** C# / .NET 8 / WPF (`WinExe`), xUnit, `AttachConsole` qua P/Invoke `kernel32`,
`System.Text.Json`. PowerShell cho kiểm thử đầu-cuối.

## Ràng buộc toàn cục

Mọi task đều phải tuân thủ, không cần nhắc lại trong từng task:

- **Không thêm bất kỳ package NuGet nào.**
- **Chú thích code viết bằng tiếng Việt.** Chuỗi hiển thị thì theo quy tắc dưới.
- **Chuỗi của chế độ không giám sát (console + báo cáo JSON + `--help`) luôn là tiếng Anh**,
  gom hết vào `ConsoleMessages.cs`, **không** đưa vào file `.resx`.
- **Không cài phần mềm thật trong bất kỳ automated test nào.** Dùng `FakeWingetService`.
- **Không sửa test cũ để cho qua.** Test cũ đỏ nghĩa là code mới làm hỏng chức năng cũ.
- **Không tự nâng quyền, không ghi registry, không đổi thiết lập Windows.**
- Package Id vẫn đi qua `PackageIdValidator` và `ProcessStartInfo.ArgumentList` — task này
  không được mở thêm đường nào cho dữ liệu người dùng chạm tới chuỗi lệnh.
- Namespace của toàn bộ file mới: `WindowsSetupAssistant.App.Cli`.
- Chạy không tham số **phải** mở giao diện y hệt hiện tại.
- Mã thoát chỉ có đúng năm giá trị: `0` xong, `1` có gói lỗi, `2` sai tham số,
  `3` thiếu WinGet, `4` bị huỷ.
- Sau mỗi task: `dotnet build` phải **0 Warning, 0 Error**; `dotnet test` phải xanh toàn bộ.

## Cấu trúc file

| File | Trách nhiệm | Task |
|---|---|---|
| `src/WindowsSetupAssistant.App/Cli/UnattendedExitCode.cs` | Enum năm mã thoát | 1 |
| `src/WindowsSetupAssistant.App/Cli/CommandLineOptions.cs` | Tham số đã phân tích | 1 |
| `src/WindowsSetupAssistant.App/Cli/CommandLineParseResult.cs` | Kết quả phân tích: Gui / Unattended / Help / Invalid | 1 |
| `src/WindowsSetupAssistant.App/Cli/CommandLineParser.cs` | Hàm thuần: `string[]` → kết quả | 1 |
| `src/WindowsSetupAssistant.App/Cli/ConsoleMessages.cs` | Toàn bộ chuỗi tiếng Anh + `BuildHelp()` | 2 |
| `src/WindowsSetupAssistant.App/Cli/IUnattendedOutput.cs` | Cổng ghi ra ngoài (test thay được) | 2 |
| `src/WindowsSetupAssistant.App/Cli/ConsoleSession.cs` | `AttachConsole` + ghi + `FreeConsole` | 2 |
| `src/WindowsSetupAssistant.App/Cli/UnattendedReport.cs` | Mô hình báo cáo + hàm dựng từ kết quả | 3 |
| `src/WindowsSetupAssistant.App/Cli/UnattendedReportWriter.cs` | Ghi JSON, không ném lỗi ra ngoài | 3 |
| `src/WindowsSetupAssistant.App/Cli/UnattendedRunner.cs` | Điều phối toàn bộ lượt chạy | 4 |
| `src/WindowsSetupAssistant.App/App.xaml.cs` | Phân nhánh khởi động | 5 |
| `tools/ui-smoke-test/Run-UnattendedSmokeTest.ps1` | Kiểm thử đầu-cuối trên bản publish | 6 |
| `README.md`, `docs/build-guide.js` | Tài liệu | 7 |

---

## Task 1: Phân tích tham số dòng lệnh

**Files:**
- Tạo: `src/WindowsSetupAssistant.App/Cli/UnattendedExitCode.cs`
- Tạo: `src/WindowsSetupAssistant.App/Cli/CommandLineOptions.cs`
- Tạo: `src/WindowsSetupAssistant.App/Cli/CommandLineParseResult.cs`
- Tạo: `src/WindowsSetupAssistant.App/Cli/CommandLineParser.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Cli/CommandLineParserTests.cs`

**Giao diện:**
- Tiêu thụ: `ExistingPackageAction` (enum sẵn có ở `WindowsSetupAssistant.Domain.Enums`,
  hai giá trị `Skip = 0`, `Upgrade = 1`).
- Cung cấp cho các task sau: `CommandLineParser.Parse(IReadOnlyList<string>? args)`
  trả về `CommandLineParseResult`; `CommandLineOptions` với ba thuộc tính
  `ProfileName`, `ExistingPackageAction`, `ReportPath`; enum `UnattendedExitCode`.

**Quy tắc phân nhánh — đọc kỹ, đây là chỗ dễ làm sai:**

| Đầu vào | Kết quả |
|---|---|
| Không có tham số nào | `Gui` — mở giao diện |
| Có `--help` / `-h` / `-?` / `/?` (ở bất kỳ vị trí nào) | `Help` |
| Có `--unattended` | `Unattended` |
| Không có `--unattended` nhưng có `--profile` / `--existing` / `--report` | `Invalid` — các cờ này vô nghĩa nếu thiếu `--unattended`, im lặng bỏ qua chúng sẽ khiến người dùng tưởng lệnh đã chạy |
| Có tham số lạ nhưng không có cờ nào của chế độ này | `Gui` — Windows đôi khi truyền tham số của riêng nó; không được vì thế mà từ chối mở ứng dụng |
| Có `--unattended` kèm tham số lạ | `Invalid` |

- [ ] **Bước 1: Viết test trước**

Tạo `tests/WindowsSetupAssistant.App.Tests/Cli/CommandLineParserTests.cs`:

```csharp
using WindowsSetupAssistant.App.Cli;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Tests.Cli;

public class CommandLineParserTests
{
    [Fact]
    public void KhongCoThamSoThiMoGiaoDien()
    {
        Assert.Equal(CommandLineMode.Gui, CommandLineParser.Parse(Array.Empty<string>()).Mode);
        Assert.Equal(CommandLineMode.Gui, CommandLineParser.Parse(null).Mode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("/?")]
    public void CacDangHelpDeuTraVeHelp(string arg)
    {
        Assert.Equal(CommandLineMode.Help, CommandLineParser.Parse(new[] { arg }).Mode);
    }

    [Fact]
    public void HelpThangTheNgayCaKhiDiKemThamSoKhac()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--help" });

        Assert.Equal(CommandLineMode.Help, result.Mode);
    }

    [Fact]
    public void ChiCoUnattendedThiDungMacDinh()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended" });

        Assert.Equal(CommandLineMode.Unattended, result.Mode);
        Assert.Null(result.Options!.ProfileName);
        Assert.Null(result.Options.ReportPath);
        Assert.Equal(ExistingPackageAction.Skip, result.Options.ExistingPackageAction);
    }

    [Fact]
    public void TenCauHinhCoDauVaKhoangTrangDuocGiuNguyen()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "Máy công ty" });

        Assert.Equal("Máy công ty", result.Options!.ProfileName);
    }

    [Fact]
    public void KhoangTrangThuaQuanhGiaTriBiCatBo()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "  May lap trinh  " });

        Assert.Equal("May lap trinh", result.Options!.ProfileName);
    }

    [Theory]
    [InlineData("skip", ExistingPackageAction.Skip)]
    [InlineData("SKIP", ExistingPackageAction.Skip)]
    [InlineData("upgrade", ExistingPackageAction.Upgrade)]
    [InlineData("Upgrade", ExistingPackageAction.Upgrade)]
    public void ExistingKhongPhanBietHoaThuong(string value, ExistingPackageAction expected)
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--existing", value });

        Assert.Equal(expected, result.Options!.ExistingPackageAction);
    }

    [Fact]
    public void ExistingGiaTriLaThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--existing", "reinstall" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("reinstall", result.ErrorMessage);
    }

    [Fact]
    public void TenCoDauGachNgangKhongBiHieuNhamLaThamSo()
    {
        // "--profile" dung ngay truoc mot co khac nghia la nguoi dung quen go gia tri.
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "--existing", "skip" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--profile", result.ErrorMessage);
    }

    [Fact]
    public void ThamSoDungCuoiCauMaThieuGiaTriThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--report" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--report", result.ErrorMessage);
    }

    [Fact]
    public void GiaTriRongBiCoiLaThieuGiaTri()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--profile", "   " });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
    }

    [Fact]
    public void ThieuUnattendedNhungCoCoRiengCuaCheDoNayThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--profile", "May ca nhan" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--unattended", result.ErrorMessage);
    }

    [Fact]
    public void ThamSoLaHoanToanKhongLamChanMoGiaoDien()
    {
        // Windows co the truyen tham so cua rieng no; khong duoc vi the ma tu choi mo ung dung.
        Assert.Equal(CommandLineMode.Gui, CommandLineParser.Parse(new[] { "/embedding" }).Mode);
    }

    [Fact]
    public void ThamSoLaDiKemUnattendedThiBaoLoi()
    {
        var result = CommandLineParser.Parse(new[] { "--unattended", "--turbo" });

        Assert.Equal(CommandLineMode.Invalid, result.Mode);
        Assert.Contains("--turbo", result.ErrorMessage);
    }

    [Fact]
    public void DayDuThamSo()
    {
        var result = CommandLineParser.Parse(new[]
        {
            "--unattended", "--profile", "May cong ty",
            "--existing", "upgrade", "--report", @"D:\bc.json"
        });

        Assert.Equal(CommandLineMode.Unattended, result.Mode);
        Assert.Equal("May cong ty", result.Options!.ProfileName);
        Assert.Equal(ExistingPackageAction.Upgrade, result.Options.ExistingPackageAction);
        Assert.Equal(@"D:\bc.json", result.Options.ReportPath);
    }

    [Fact]
    public void MaThoatCoDungNamGiaTriTheoSpec()
    {
        Assert.Equal(0, (int)UnattendedExitCode.Success);
        Assert.Equal(1, (int)UnattendedExitCode.SomePackagesFailed);
        Assert.Equal(2, (int)UnattendedExitCode.InvalidArguments);
        Assert.Equal(3, (int)UnattendedExitCode.WingetMissing);
        Assert.Equal(4, (int)UnattendedExitCode.Cancelled);
        Assert.Equal(5, Enum.GetValues<UnattendedExitCode>().Length);
    }
}
```

- [ ] **Bước 2: Chạy để chắc chắn test đỏ**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter CommandLineParserTests
```

Kỳ vọng: **lỗi biên dịch** vì chưa có `CommandLineParser` — đúng như mong đợi.

- [ ] **Bước 3: Viết `UnattendedExitCode.cs`**

```csharp
namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Mã thoát trả về cho script gọi ứng dụng. Đây là HỢP ĐỒNG với bên ngoài:
/// không đổi giá trị số, không thêm giá trị mới nếu chưa cập nhật README.
/// </summary>
public enum UnattendedExitCode
{
    /// <summary>Mọi gói thành công (gói bỏ qua vì đã có sẵn cũng tính là thành công).</summary>
    Success = 0,

    /// <summary>Chạy hết hàng đợi nhưng có ít nhất một gói lỗi.</summary>
    SomePackagesFailed = 1,

    /// <summary>Tham số sai, hoặc không tìm thấy cấu hình theo tên.</summary>
    InvalidArguments = 2,

    /// <summary>Máy không có WinGet.</summary>
    WingetMissing = 3,

    /// <summary>Người dùng nhấn Ctrl+C.</summary>
    Cancelled = 4
}
```

- [ ] **Bước 4: Viết `CommandLineOptions.cs`**

```csharp
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>Tham số của một lượt chạy không giám sát, sau khi đã phân tích xong.</summary>
public sealed class CommandLineOptions
{
    /// <summary>Tên cấu hình cần cài. null nghĩa là dùng cấu hình đang chọn trong file dữ liệu.</summary>
    public string? ProfileName { get; init; }

    /// <summary>Gói đã có trên máy: bỏ qua hay nâng cấp.</summary>
    public ExistingPackageAction ExistingPackageAction { get; init; } = ExistingPackageAction.Skip;

    /// <summary>Đường dẫn file báo cáo. null nghĩa là tự sinh trong thư mục Reports cạnh .exe.</summary>
    public string? ReportPath { get; init; }
}
```

- [ ] **Bước 5: Viết `CommandLineParseResult.cs`**

```csharp
namespace WindowsSetupAssistant.App.Cli;

/// <summary>Ứng dụng phải làm gì sau khi đọc xong tham số.</summary>
public enum CommandLineMode
{
    /// <summary>Mở giao diện đồ hoạ như bình thường.</summary>
    Gui,

    /// <summary>Chạy lượt cài đặt không giám sát.</summary>
    Unattended,

    /// <summary>In hướng dẫn rồi thoát.</summary>
    Help,

    /// <summary>Tham số sai: in lỗi kèm hướng dẫn rồi thoát mã 2.</summary>
    Invalid
}

/// <summary>Kết quả phân tích tham số. Bất biến, không có I/O.</summary>
public sealed class CommandLineParseResult
{
    private CommandLineParseResult(CommandLineMode mode) => Mode = mode;

    public CommandLineMode Mode { get; }

    /// <summary>Chỉ khác null khi <see cref="Mode"/> là <see cref="CommandLineMode.Unattended"/>.</summary>
    public CommandLineOptions? Options { get; private init; }

    /// <summary>Chỉ khác null khi <see cref="Mode"/> là <see cref="CommandLineMode.Invalid"/>.</summary>
    public string? ErrorMessage { get; private init; }

    public static CommandLineParseResult Gui() => new(CommandLineMode.Gui);

    public static CommandLineParseResult Help() => new(CommandLineMode.Help);

    public static CommandLineParseResult Unattended(CommandLineOptions options) =>
        new(CommandLineMode.Unattended) { Options = options };

    public static CommandLineParseResult Invalid(string message) =>
        new(CommandLineMode.Invalid) { ErrorMessage = message };
}
```

- [ ] **Bước 6: Viết `CommandLineParser.cs`**

```csharp
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Phân tích tham số dòng lệnh. Hàm thuần: không đọc file, không gọi WinGet,
/// nên kiểm thử được đầy đủ mọi tổ hợp mà không cần môi trường thật.
/// </summary>
public static class CommandLineParser
{
    private const string UnattendedFlag = "--unattended";
    private const string ProfileFlag = "--profile";
    private const string ExistingFlag = "--existing";
    private const string ReportFlag = "--report";

    public static CommandLineParseResult Parse(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return CommandLineParseResult.Gui();
        }

        // --help thắng tất cả: người dùng đang bối rối thì đừng bắt họ sửa lệnh cho đúng
        // rồi mới được xem hướng dẫn.
        foreach (var arg in args)
        {
            if (IsHelpFlag(arg))
            {
                return CommandLineParseResult.Help();
            }
        }

        var unattended = false;
        var sawOwnFlag = false;
        string? profileName = null;
        string? reportPath = null;
        var existingAction = ExistingPackageAction.Skip;

        for (var index = 0; index < args.Count; index++)
        {
            var flag = args[index].Trim();

            if (Matches(flag, UnattendedFlag))
            {
                unattended = true;
                sawOwnFlag = true;
                continue;
            }

            if (Matches(flag, ProfileFlag))
            {
                sawOwnFlag = true;

                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ProfileFlag);
                }

                profileName = value;
                continue;
            }

            if (Matches(flag, ReportFlag))
            {
                sawOwnFlag = true;

                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ReportFlag);
                }

                reportPath = value;
                continue;
            }

            if (Matches(flag, ExistingFlag))
            {
                sawOwnFlag = true;

                if (!TryTakeValue(args, ref index, out var value))
                {
                    return MissingValue(ExistingFlag);
                }

                if (string.Equals(value, "skip", StringComparison.OrdinalIgnoreCase))
                {
                    existingAction = ExistingPackageAction.Skip;
                }
                else if (string.Equals(value, "upgrade", StringComparison.OrdinalIgnoreCase))
                {
                    existingAction = ExistingPackageAction.Upgrade;
                }
                else
                {
                    return CommandLineParseResult.Invalid(
                        $"Unknown value for {ExistingFlag}: \"{value}\". Use \"skip\" or \"upgrade\".");
                }

                continue;
            }

            // Tham số lạ. Nếu người dùng KHÔNG hề dùng cờ nào của chế độ này thì rất có thể
            // đây là tham số do Windows tự truyền vào - cứ mở giao diện, đừng chặn họ lại.
            if (!sawOwnFlag && !unattended)
            {
                return CommandLineParseResult.Gui();
            }

            return CommandLineParseResult.Invalid($"Unknown argument: \"{args[index]}\".");
        }

        if (!unattended)
        {
            return CommandLineParseResult.Invalid(
                $"{ProfileFlag} / {ExistingFlag} / {ReportFlag} only work together with {UnattendedFlag}.");
        }

        return CommandLineParseResult.Unattended(new CommandLineOptions
        {
            ProfileName = profileName,
            ExistingPackageAction = existingAction,
            ReportPath = reportPath
        });
    }

    private static bool IsHelpFlag(string arg)
    {
        var trimmed = arg.Trim();

        return Matches(trimmed, "--help")
            || Matches(trimmed, "-h")
            || trimmed == "-?"
            || trimmed == "/?";
    }

    private static bool Matches(string arg, string flag) =>
        string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);

    private static CommandLineParseResult MissingValue(string flag) =>
        CommandLineParseResult.Invalid($"Missing value after {flag}.");

    /// <summary>
    /// Lấy giá trị đứng ngay sau một cờ. Giá trị bắt đầu bằng "-" được coi là cờ tiếp theo
    /// chứ không phải giá trị - trường hợp người dùng quên gõ giá trị.
    /// </summary>
    private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        value = string.Empty;

        if (index + 1 >= args.Count)
        {
            return false;
        }

        var candidate = args[index + 1];

        if (candidate.StartsWith('-') || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        index++;
        value = candidate.Trim();
        return true;
    }
}
```

- [ ] **Bước 7: Chạy test cho xanh**

```bash
dotnet build && dotnet test tests/WindowsSetupAssistant.App.Tests --filter CommandLineParserTests
```

Kỳ vọng: 18 test mới xanh (một số là `[Theory]` nên số ca chạy nhiều hơn số hàm).

- [ ] **Bước 8: Chạy toàn bộ để chắc chắn không làm hỏng gì**

```bash
dotnet test
```

Kỳ vọng: toàn bộ test cũ vẫn xanh, cộng thêm các ca mới của task này.

- [ ] **Bước 9: Commit**

```bash
git add src/WindowsSetupAssistant.App/Cli tests/WindowsSetupAssistant.App.Tests/Cli
git commit -m "feat(cli): phan tich tham so dong lenh cho che do khong giam sat"
```

---

## Task 2: Nói chuyện với cửa sổ lệnh

**Files:**
- Tạo: `src/WindowsSetupAssistant.App/Cli/IUnattendedOutput.cs`
- Tạo: `src/WindowsSetupAssistant.App/Cli/ConsoleMessages.cs`
- Tạo: `src/WindowsSetupAssistant.App/Cli/ConsoleSession.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Cli/ConsoleMessagesTests.cs`

**Giao diện:**
- Tiêu thụ: `UnattendedExitCode` (Task 1).
- Cung cấp: `IUnattendedOutput` với `WriteLine(string)` và `WriteError(string)`;
  `ConsoleSession.Attach()` trả về một `ConsoleSession` (hiện thực `IUnattendedOutput`,
  `IDisposable`); `ConsoleMessages` với các hàm dựng chuỗi mà Task 4 sẽ gọi.

**Vì sao cần `IUnattendedOutput`:** `ConsoleSession` gọi P/Invoke nên không kiểm thử được
trong test runner. Tách cổng ra giúp Task 4 kiểm chứng *nội dung* in ra mà không cần console thật.

**Bẫy kỹ thuật quan trọng:** ứng dụng `WinExe` khởi động không có console, nên `Console.Out`
trỏ vào chỗ trống. Gọi `AttachConsole` xong **vẫn phải** gán lại `Console.Out` / `Console.Error`
bằng `Console.OpenStandardOutput()` / `OpenStandardError()`, nếu không sẽ không có chữ nào
hiện ra dù hàm `AttachConsole` báo thành công.

- [ ] **Bước 1: Viết test trước**

Tạo `tests/WindowsSetupAssistant.App.Tests/Cli/ConsoleMessagesTests.cs`:

```csharp
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
```

- [ ] **Bước 2: Chạy để chắc chắn test đỏ**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter ConsoleMessagesTests
```

Kỳ vọng: lỗi biên dịch vì chưa có `ConsoleMessages`.

- [ ] **Bước 3: Viết `IUnattendedOutput.cs`**

```csharp
namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Nơi chế độ không giám sát ghi chữ ra. Tách thành interface để test kiểm chứng được
/// nội dung mà không cần console thật (ConsoleSession dùng P/Invoke, không chạy trong test runner).
/// </summary>
public interface IUnattendedOutput
{
    void WriteLine(string text = "");

    void WriteError(string text);
}
```

- [ ] **Bước 4: Viết `ConsoleMessages.cs`**

```csharp
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

    public static string WingetDetected(string? version) =>
        $"WinGet {version ?? "(unknown version)"} detected.";

    public static string ProfileSummary(string profileName, int total, int notInstalled) =>
        $"Profile \"{profileName}\": {total} package(s), {notInstalled} not installed.";

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
```

- [ ] **Bước 5: Viết `ConsoleSession.cs`**

```csharp
using System.IO;
using System.Runtime.InteropServices;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Mượn cửa sổ lệnh của tiến trình cha để in chữ ra.
///
/// Ứng dụng được build ở dạng WinExe nên KHÔNG có console riêng. AttachConsole(-1) gắn vào
/// console của tiến trình cha - tức đúng cửa sổ cmd/PowerShell mà người dùng đang gõ.
/// Khi không có cha nào có console (bấm đúp vào .exe, chạy từ Task Scheduler) thì hàm này
/// thất bại: đó KHÔNG phải lỗi, ứng dụng vẫn chạy và vẫn ghi báo cáo, chỉ là không in ra đâu.
/// </summary>
public sealed class ConsoleSession : IUnattendedOutput, IDisposable
{
    private const int AttachParentProcess = -1;

    private readonly bool _attached;
    private bool _disposed;

    private ConsoleSession(bool attached) => _attached = attached;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    public bool IsAttached => _attached;

    public static ConsoleSession Attach()
    {
        bool attached;

        try
        {
            attached = AttachConsole(AttachParentProcess);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            attached = false;
        }

        if (attached)
        {
            RebindStandardStreams();
        }

        return new ConsoleSession(attached);
    }

    /// <summary>
    /// BẮT BUỘC sau khi gắn console. Tiến trình WinExe khởi động với Console.Out trỏ vào
    /// chỗ trống; không gán lại thì AttachConsole báo thành công nhưng không chữ nào hiện ra.
    /// </summary>
    private static void RebindStandardStreams()
    {
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch (IOException)
        {
            // Console cha đã đóng giữa chừng - không in được nhưng cũng không được làm sập.
        }
    }

    public void WriteLine(string text = "")
    {
        if (!_attached || _disposed)
        {
            return;
        }

        try
        {
            Console.Out.WriteLine(text);
        }
        catch (IOException)
        {
        }
    }

    public void WriteError(string text)
    {
        if (!_attached || _disposed)
        {
            return;
        }

        try
        {
            Console.Error.WriteLine(text);
        }
        catch (IOException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_attached)
        {
            return;
        }

        try
        {
            Console.Out.Flush();
            Console.Error.Flush();
            FreeConsole();
        }
        catch (IOException)
        {
        }
    }
}
```

- [ ] **Bước 6: Chạy test**

```bash
dotnet build && dotnet test tests/WindowsSetupAssistant.App.Tests --filter ConsoleMessagesTests
```

Kỳ vọng: 7 test mới xanh.

- [ ] **Bước 7: Chạy toàn bộ**

```bash
dotnet test
```

- [ ] **Bước 8: Commit**

```bash
git add src/WindowsSetupAssistant.App/Cli tests/WindowsSetupAssistant.App.Tests/Cli
git commit -m "feat(cli): gan console cha va gom chuoi tieng Anh cua che do khong giam sat"
```

---

## Task 3: Báo cáo JSON

**Files:**
- Tạo: `src/WindowsSetupAssistant.App/Cli/UnattendedReport.cs`
- Tạo: `src/WindowsSetupAssistant.App/Cli/UnattendedReportWriter.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Cli/UnattendedReportTests.cs`
- Sửa: `tests/WindowsSetupAssistant.App.Tests/WindowsSetupAssistant.App.Tests.csproj`

**Giao diện:**
- Tiêu thụ: `InstallationRunSummary` (có `Results`, `WasCancelled`, `TotalDuration`,
  và các thuộc tính đếm `SucceededCount` / `SkippedCount` / `FailedCount` / `CancelledCount`);
  `InstallationResult` (có `PackageId`, `DisplayName`, `Outcome`, `ExitCode`, `Message`
  kiểu `LocalizedText`, `Duration`); `IStringLocalizer.Format(LocalizedText, CultureInfo)`;
  `WingetExitCodes.RequiresReboot(int)`; `UnattendedExitCode` (Task 1).
- Cung cấp: `UnattendedReport.Create(...)`; `UnattendedReportWriter.TryWrite(report, path, out error)`;
  `UnattendedReportWriter.DefaultPath(DateTimeOffset)`.

**Vì sao đây là task riêng:** tên trường JSON là hợp đồng với script của người khác. Đổi một
tên trường là làm hỏng script của họ trong khi build vẫn xanh — nên nó cần bộ test riêng canh giữ.

- [ ] **Bước 1: Cho test project thấy các fake sẵn có**

Sửa `tests/WindowsSetupAssistant.App.Tests/WindowsSetupAssistant.App.Tests.csproj`, trong
`<ItemGroup>` đang có dòng `RecordingLogger.cs`, thêm hai dòng:

```xml
    <Compile Include="..\WindowsSetupAssistant.Tests\Fakes\FakeWingetService.cs" Link="Fakes\FakeWingetService.cs" />
    <Compile Include="..\WindowsSetupAssistant.Tests\Fakes\StubLocalizer.cs" Link="Fakes\StubLocalizer.cs" />
```

`FakeWingetService` dùng ở Task 4; thêm luôn ở đây để chỉ phải sửa csproj một lần.

- [ ] **Bước 2: Viết test trước**

Tạo `tests/WindowsSetupAssistant.App.Tests/Cli/UnattendedReportTests.cs`:

```csharp
using System.Globalization;
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
```

- [ ] **Bước 3: Chạy để chắc chắn test đỏ**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter UnattendedReportTests
```

- [ ] **Bước 4: Viết `UnattendedReport.cs`**

```csharp
using System.Globalization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Infrastructure.Winget;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>Số lượng theo từng loại kết quả.</summary>
public sealed class UnattendedReportCounts
{
    public int Total { get; init; }
    public int Succeeded { get; init; }
    public int Upgraded { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int Cancelled { get; init; }
}

/// <summary>Một dòng kết quả cho đúng một gói.</summary>
public sealed class UnattendedReportPackage
{
    public string Name { get; init; } = string.Empty;
    public string PackageId { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public int? ExitCode { get; init; }
    public double DurationSeconds { get; init; }
    public bool RestartRequired { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Báo cáo một lượt chạy không giám sát.
///
/// Tên các thuộc tính ở đây trở thành tên trường JSON, tức là HỢP ĐỒNG với script của người
/// khác. Đổi tên một trường là làm hỏng script của họ mà build vẫn xanh - vì vậy có bộ test
/// riêng canh giữ tên trường.
/// </summary>
public sealed class UnattendedReport
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public double DurationSeconds { get; init; }
    public string MachineName { get; init; } = string.Empty;
    public string? WingetVersion { get; init; }
    public string ProfileName { get; init; } = string.Empty;
    public string ExistingPackageAction { get; init; } = nameof(Domain.Enums.ExistingPackageAction.Skip);
    public int ExitCode { get; init; }
    public bool WasCancelled { get; init; }
    public UnattendedReportCounts Counts { get; init; } = new();
    public IReadOnlyList<UnattendedReportPackage> Packages { get; init; } = Array.Empty<UnattendedReportPackage>();

    public static UnattendedReport Create(
        InstallationRunSummary summary,
        string profileName,
        string? wingetVersion,
        ExistingPackageAction existingPackageAction,
        UnattendedExitCode exitCode,
        DateTimeOffset startedAt,
        IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(localizer);

        // Báo cáo luôn tiếng Anh, không theo ngôn ngữ giao diện - xem mục 7 của spec.
        var english = CultureInfo.GetCultureInfo("en");

        var packages = summary.Results.Select(result => new UnattendedReportPackage
        {
            Name = result.DisplayName,
            PackageId = result.PackageId,
            Outcome = result.Outcome.ToString(),
            ExitCode = result.ExitCode,
            DurationSeconds = Math.Round(result.Duration.TotalSeconds, 1),
            RestartRequired = result.ExitCode is not null && WingetExitCodes.RequiresReboot(result.ExitCode.Value),
            ErrorMessage = result.Outcome == InstallOutcome.Failed
                ? localizer.Format(result.Message, english)
                : null
        }).ToList();

        return new UnattendedReport
        {
            StartedAt = startedAt,
            FinishedAt = startedAt + summary.TotalDuration,
            DurationSeconds = Math.Round(summary.TotalDuration.TotalSeconds, 1),
            MachineName = Environment.MachineName,
            WingetVersion = wingetVersion,
            ProfileName = profileName,
            ExistingPackageAction = existingPackageAction.ToString(),
            ExitCode = (int)exitCode,
            WasCancelled = summary.WasCancelled,
            Counts = new UnattendedReportCounts
            {
                Total = summary.Results.Count,
                Succeeded = summary.Results.Count(r => r.Outcome == InstallOutcome.Succeeded),
                Upgraded = summary.Results.Count(r => r.Outcome == InstallOutcome.Upgraded),
                Skipped = summary.SkippedCount,
                Failed = summary.FailedCount,
                Cancelled = summary.CancelledCount
            },
            Packages = packages
        };
    }
}
```

> Lưu ý: `InstallationRunSummary.SucceededCount` gộp cả `Succeeded` và `Upgraded`, nên báo cáo
> đếm riêng hai loại thay vì dùng thẳng thuộc tính đó.

- [ ] **Bước 5: Viết `UnattendedReportWriter.cs`**

```csharp
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Ghi báo cáo ra file JSON. KHÔNG ném lỗi ra ngoài: ghi báo cáo thất bại không được phép
/// làm đổi mã thoát của lượt cài - phần mềm đã cài xong vẫn là đã cài xong.
/// </summary>
public static class UnattendedReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(UnattendedReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return JsonSerializer.Serialize(report, Options);
    }

    /// <summary>Đường dẫn mặc định: Reports\unattended-yyyyMMdd-HHmmss.json cạnh file .exe.</summary>
    public static string DefaultPath(DateTimeOffset timestamp) => Path.Combine(
        AppContext.BaseDirectory,
        "Reports",
        $"unattended-{timestamp.LocalDateTime:yyyyMMdd-HHmmss}.json");

    public static bool TryWrite(UnattendedReport report, string path, out string? error)
    {
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, Serialize(report));
            error = null;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            error = exception.Message;
            return false;
        }
    }
}
```

- [ ] **Bước 6: Chạy test cho xanh**

```bash
dotnet build && dotnet test tests/WindowsSetupAssistant.App.Tests --filter UnattendedReportTests
```

Kỳ vọng: 6 test mới xanh.

- [ ] **Bước 7: Chạy toàn bộ**

```bash
dotnet test
```

- [ ] **Bước 8: Commit**

```bash
git add src/WindowsSetupAssistant.App/Cli tests/WindowsSetupAssistant.App.Tests
git commit -m "feat(cli): bao cao JSON cho luot chay khong giam sat"
```

---

## Task 4: Bộ điều phối lượt chạy

**Files:**
- Tạo: `src/WindowsSetupAssistant.App/Cli/UnattendedRunner.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Cli/UnattendedRunnerTests.cs`

**Giao diện:**
- Tiêu thụ: `IWingetService`, `IProfileRepository`, `InstallationQueueService.RunAsync(packages, options, progress, ct)`,
  `IStringLocalizer`, `IUnattendedOutput` (Task 2), `UnattendedReport` (Task 3),
  `CommandLineOptions` + `UnattendedExitCode` (Task 1).
- Cung cấp: `UnattendedRunner.RunAsync(CommandLineOptions, CancellationToken)` → `Task<UnattendedExitCode>`.

**Quyết định thiết kế cần giữ:** runner **không tự lọc bỏ gói đã cài**. Nó truyền toàn bộ gói
đang được tick vào `InstallationQueueService` kèm `PreCheckedInstalledPackageIds`, để chính
hàng đợi quyết định bỏ qua hay nâng cấp. Nhờ vậy hai chế độ dùng chung một bộ quy tắc, không
thể lệch nhau. Danh sách gói đã cài vẫn cần lấy để in dòng tổng quan lúc bắt đầu.

- [ ] **Bước 1: Viết test trước**

Tạo `tests/WindowsSetupAssistant.App.Tests/Cli/UnattendedRunnerTests.cs`:

```csharp
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
}
```

- [ ] **Bước 2: Bổ sung khả năng cho `FakeWingetService`**

Test ở trên dùng `_winget.Availability`, mà bản fake hiện tại trả cứng "có sẵn". Mở
`tests/WindowsSetupAssistant.Tests/Fakes/FakeWingetService.cs`, thêm thuộc tính và sửa
đúng một hàm:

```csharp
    /// <summary>Cho test mô phỏng máy không có winget.</summary>
    public WingetAvailability Availability { get; set; } = WingetAvailability.Available("v1.9.0 (fake)");

    public Task<WingetAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Availability);
```

Đây là **bổ sung**, không phải sửa test cũ để lách: giá trị mặc định giữ nguyên hành vi cũ
nên mọi test đang dùng bản fake này vẫn chạy y hệt.

- [ ] **Bước 3: Chạy để chắc chắn test đỏ**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter UnattendedRunnerTests
```

- [ ] **Bước 4: Viết `UnattendedRunner.cs`**

```csharp
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Cli;

/// <summary>
/// Chạy trọn một lượt cài đặt không giám sát.
///
/// Lớp này CỐ Ý không tự gọi WinGet và không tự quyết định bỏ qua hay nâng cấp. Nó dùng đúng
/// InstallationQueueService mà giao diện đang dùng, nên quy tắc cài đặt của hai chế độ
/// không thể lệch nhau.
/// </summary>
public sealed class UnattendedRunner
{
    private readonly IWingetService _wingetService;
    private readonly IProfileRepository _repository;
    private readonly InstallationQueueService _queueService;
    private readonly IStringLocalizer _localizer;
    private readonly IUnattendedOutput _output;
    private readonly IAppLogger _logger;

    public UnattendedRunner(
        IWingetService wingetService,
        IProfileRepository repository,
        InstallationQueueService queueService,
        IStringLocalizer localizer,
        IUnattendedOutput output,
        IAppLogger logger)
    {
        _wingetService = wingetService ?? throw new ArgumentNullException(nameof(wingetService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UnattendedExitCode> RunAsync(
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startedAt = DateTimeOffset.Now;

        _output.WriteLine(ConsoleMessages.ModeBanner);

        var availability = await _wingetService.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);

        if (!availability.IsAvailable)
        {
            _output.WriteError(ConsoleMessages.WingetMissing);
            return UnattendedExitCode.WingetMissing;
        }

        _output.WriteLine(ConsoleMessages.WingetDetected(availability.Version));

        var catalog = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);

        // Repository âm thầm trả về danh sách mẫu khi không đọc được file. Ở giao diện thì
        // người dùng nhìn thấy ngay là danh sách lạ; ở đây không ai nhìn cả, nên phải nói to.
        if (!File.Exists(_repository.DataFilePath))
        {
            _output.WriteError(ConsoleMessages.DataFileMissing(_repository.DataFilePath));
        }

        var profile = ResolveProfile(catalog, options.ProfileName);

        if (profile is null)
        {
            _output.WriteError(ConsoleMessages.ProfileNotFound(
                options.ProfileName ?? string.Empty,
                catalog.Profiles.Select(p => p.Name)));

            return UnattendedExitCode.InvalidArguments;
        }

        var selected = profile.Packages.Where(package => package.IsSelected).ToList();

        var installedIds = await GetInstalledIdsAsync(cancellationToken).ConfigureAwait(false);
        var notInstalled = selected.Count(package => !installedIds.Contains(package.PackageId));

        _output.WriteLine(ConsoleMessages.ProfileSummary(profile.Name, selected.Count, notInstalled));

        if (selected.Count == 0 ||
            (options.ExistingPackageAction == ExistingPackageAction.Skip && notInstalled == 0))
        {
            _output.WriteLine(ConsoleMessages.NothingToDo);
        }

        var summary = await _queueService.RunAsync(
            selected,
            new InstallationOptions
            {
                ExistingPackageAction = options.ExistingPackageAction,
                PreCheckedInstalledPackageIds = installedIds
            },
            new Progress<InstallationProgressUpdate>(ReportProgress),
            cancellationToken).ConfigureAwait(false);

        var exitCode = summary.WasCancelled
            ? UnattendedExitCode.Cancelled
            : summary.FailedCount > 0
                ? UnattendedExitCode.SomePackagesFailed
                : UnattendedExitCode.Success;

        _output.WriteLine();
        _output.WriteLine(ConsoleMessages.Elapsed(summary.TotalDuration));
        _output.WriteLine(ConsoleMessages.Totals(
            succeeded: summary.Results.Count(r => r.Outcome == InstallOutcome.Succeeded),
            upgraded: summary.Results.Count(r => r.Outcome == InstallOutcome.Upgraded),
            skipped: summary.SkippedCount,
            failed: summary.FailedCount,
            cancelled: summary.CancelledCount));

        WriteReport(summary, profile.Name, availability.Version, options, exitCode, startedAt);

        _output.WriteLine(ConsoleMessages.ExitLine(exitCode));

        return exitCode;
    }

    /// <summary>
    /// Tìm cấu hình theo tên, bỏ qua hoa thường và khoảng trắng thừa - người gõ lệnh
    /// không nên bị đánh trượt chỉ vì gõ thiếu một chữ hoa.
    /// </summary>
    private static InstallationProfile? ResolveProfile(SoftwareCatalog catalog, string? requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return catalog.GetActiveProfile();
        }

        var wanted = requestedName.Trim();

        return catalog.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Name?.Trim(), wanted, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlySet<string>> GetInstalledIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installed = await _wingetService.GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);

            return installed
                .Select(row => row.PackageId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Không quét được thì cứ để hàng đợi tự hỏi từng gói - chậm hơn nhưng vẫn đúng.
            _logger.Warning($"Could not list installed packages: {exception.Message}");
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void ReportProgress(InstallationProgressUpdate update)
    {
        if (update.CurrentPackage is not null && update.CompletedResult is null)
        {
            _output.WriteLine(ConsoleMessages.PackageStarting(
                update.CompletedCount + 1,
                update.TotalCount,
                update.CurrentPackage.PackageId));

            return;
        }

        if (update.CompletedResult is not { } result)
        {
            return;
        }

        if (result.Outcome == InstallOutcome.Failed)
        {
            _output.WriteError(ConsoleMessages.PackageFailed(
                result.PackageId,
                result.ExitCode,
                _localizer.Format(result.Message, System.Globalization.CultureInfo.GetCultureInfo("en"))));

            return;
        }

        _output.WriteLine(ConsoleMessages.PackageFinished(result.Outcome.ToString(), result.Duration));
    }

    private void WriteReport(
        InstallationRunSummary summary,
        string profileName,
        string? wingetVersion,
        CommandLineOptions options,
        UnattendedExitCode exitCode,
        DateTimeOffset startedAt)
    {
        var report = UnattendedReport.Create(
            summary,
            profileName,
            wingetVersion,
            options.ExistingPackageAction,
            exitCode,
            startedAt,
            _localizer);

        var path = options.ReportPath ?? UnattendedReportWriter.DefaultPath(startedAt);

        if (UnattendedReportWriter.TryWrite(report, path, out var error))
        {
            _output.WriteLine(ConsoleMessages.ReportWritten(Path.GetFullPath(path)));
            return;
        }

        // Ghi báo cáo hỏng KHÔNG được đổi mã thoát: việc cài đã xong rồi.
        _output.WriteError(ConsoleMessages.ReportFailed(path, error ?? "unknown error"));
    }
}
```

- [ ] **Bước 5: Chạy test cho xanh**

```bash
dotnet build && dotnet test tests/WindowsSetupAssistant.App.Tests --filter UnattendedRunnerTests
```

Kỳ vọng: 15 test mới xanh.

- [ ] **Bước 6: Chạy toàn bộ**

```bash
dotnet test
```

Kỳ vọng: mọi test cũ vẫn xanh — đặc biệt là các test của `InstallationQueueService`, vì
task này chỉ *dùng* nó chứ không sửa.

- [ ] **Bước 7: Commit**

```bash
git add src/WindowsSetupAssistant.App/Cli tests/WindowsSetupAssistant.App.Tests tests/WindowsSetupAssistant.Tests/Fakes/FakeWingetService.cs
git commit -m "feat(cli): bo dieu phoi luot chay khong giam sat"
```

---

## Task 5: Nối vào lúc khởi động ứng dụng

**Files:**
- Sửa: `src/WindowsSetupAssistant.App/App.xaml.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Cli/AppStartupBranchTests.cs`

**Giao diện:**
- Tiêu thụ: toàn bộ Task 1–4.
- Cung cấp: hành vi khởi động phân nhánh. Không có API mới cho task sau.

**Ba cái bẫy phải xử lý — bỏ sót cái nào cũng làm treo máy đang chạy không người trông:**

1. **`ShutdownMode`.** Mặc định WPF là `OnLastWindowClose`. Nhánh không giám sát không tạo
   cửa sổ nào, nên phải đặt `ShutdownMode.OnExplicitShutdown` **trước** khi chạy, nếu không
   tiến trình có thể tự thoát giữa chừng.
2. **Hộp thoại lỗi.** `OnDispatcherUnhandledException` hiện đang gọi `MessageBox.Show`. Một
   hộp thoại đứng chờ người bấm trong chế độ không giám sát sẽ treo máy đến sáng hôm sau.
   Phải chuyển sang in ra console và thoát.
3. **Không được `await` trong `OnStartup`.** `OnStartup` là hàm đồng bộ. Khởi chạy tác vụ rồi
   để `OnStartup` trả về; vòng lặp thông điệp của WPF sẽ bơm tiếp các đoạn `await` sau đó.
   Đây chính là lý do bước 1 bắt buộc.

- [ ] **Bước 1: Viết test trước**

Tạo `tests/WindowsSetupAssistant.App.Tests/Cli/AppStartupBranchTests.cs`:

```csharp
using System.Reflection;
using WindowsSetupAssistant.App.Cli;

namespace WindowsSetupAssistant.App.Tests.Cli;

/// <summary>
/// Canh giu cac quyet dinh khoi dong ma neu lam sai se chi lo ra khi chay ban publish that.
/// </summary>
public class AppStartupBranchTests
{
    private static string ReadAppSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "WindowsSetupAssistant.App", "App.xaml.cs"));
    }

    [Fact]
    public void OnStartupPhanNhanhTheoThamSoDongLenh()
    {
        var source = ReadAppSource();

        Assert.Contains("CommandLineParser.Parse(e.Args)", source);
    }

    [Fact]
    public void CheDoKhongGiamSatDatShutdownModeTuongMinh()
    {
        // Khong co cua so nao, nen de mac dinh OnLastWindowClose thi tien trinh co the
        // tu thoat truoc khi cai xong.
        Assert.Contains("ShutdownMode.OnExplicitShutdown", ReadAppSource());
    }

    [Fact]
    public void KhongHienMessageBoxKhiChayKhongGiamSat()
    {
        var source = ReadAppSource();
        var handlerIndex = source.IndexOf("OnDispatcherUnhandledException", StringComparison.Ordinal);

        Assert.True(handlerIndex > 0);

        // Trinh xu ly loi phai kiem tra co che do truoc khi nghi den viec hien hop thoai.
        var handler = source[handlerIndex..];
        var messageBoxIndex = handler.IndexOf("MessageBox.Show", StringComparison.Ordinal);
        var guardIndex = handler.IndexOf("_unattendedOutput", StringComparison.Ordinal);

        Assert.True(guardIndex > 0, "Trinh xu ly loi phai biet dang chay o che do khong giam sat.");
        Assert.True(guardIndex < messageBoxIndex, "Phai kiem tra che do TRUOC khi hien MessageBox.");
    }

    [Fact]
    public void CoDangKyCtrlC()
    {
        Assert.Contains("CancelKeyPress", ReadAppSource());
    }

    [Fact]
    public void MoiNhanhDeuKetThucBangShutdownCoMaThoat()
    {
        Assert.Contains("Shutdown((int)", ReadAppSource());
    }
}
```

> Đây là test đọc mã nguồn, không phải test hành vi — cố ý như vậy. Không thể khởi động hai
> lần `System.Windows.Application` trong một test runner, mà ba quyết định trên nếu làm sai
> thì chỉ lộ ra khi chạy bản publish thật. Task 6 mới là bản kiểm chứng hành vi thật.

- [ ] **Bước 2: Chạy để chắc chắn test đỏ**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter AppStartupBranchTests
```

Kỳ vọng: các test về `ShutdownMode`, `CancelKeyPress`, `Shutdown((int)` **TRƯỢT**.

- [ ] **Bước 3: Sửa `App.xaml.cs` — thêm trường và phân nhánh trong `OnStartup`**

Thêm trường ngay cạnh `_mainViewModel`:

```csharp
    private MainViewModel? _mainViewModel;

    /// <summary>Khác null nghĩa là đang chạy chế độ không giám sát: tuyệt đối không hiện hộp thoại.</summary>
    private IUnattendedOutput? _unattendedOutput;
```

Ngay sau `base.OnStartup(e);` ở đầu `OnStartup`, chèn khối phân nhánh:

```csharp
        base.OnStartup(e);

        var parseResult = CommandLineParser.Parse(e.Args);

        if (parseResult.Mode != CommandLineMode.Gui)
        {
            RunCommandLine(parseResult);
            return;
        }

        // ... toàn bộ phần dựng giao diện hiện có giữ nguyên, không đổi một dòng nào ...
```

- [ ] **Bước 4: Thêm hai hàm mới vào `App.xaml.cs`**

```csharp
    /// <summary>
    /// Nhánh dòng lệnh. KHÔNG tạo cửa sổ nào.
    ///
    /// OnStartup là hàm đồng bộ nên không await được ở đây: ta khởi chạy tác vụ rồi trả về,
    /// vòng lặp thông điệp của WPF sẽ bơm tiếp các đoạn await sau đó. Điều đó chỉ đúng khi
    /// ShutdownMode là OnExplicitShutdown - mặc định OnLastWindowClose sẽ đóng ứng dụng ngay
    /// vì không có cửa sổ nào cả.
    /// </summary>
    private void RunCommandLine(CommandLineParseResult parseResult)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _ = RunCommandLineAsync(parseResult);
    }

    private async Task RunCommandLineAsync(CommandLineParseResult parseResult)
    {
        var console = ConsoleSession.Attach();
        _unattendedOutput = console;

        var exitCode = UnattendedExitCode.InvalidArguments;
        using var cancellation = new CancellationTokenSource();

        ConsoleCancelEventHandler? cancelHandler = null;

        try
        {
            switch (parseResult.Mode)
            {
                case CommandLineMode.Help:
                    console.WriteLine(ConsoleMessages.BuildHelp());
                    exitCode = UnattendedExitCode.Success;
                    break;

                case CommandLineMode.Invalid:
                    console.WriteError(parseResult.ErrorMessage ?? "Invalid arguments.");
                    console.WriteLine();
                    console.WriteLine(ConsoleMessages.BuildHelp());
                    exitCode = UnattendedExitCode.InvalidArguments;
                    break;

                default:
                    cancelHandler = (_, args) =>
                    {
                        // Cancel = true để Windows không giết tiến trình ngay: ta cần kịp
                        // dừng gói đang cài và ghi báo cáo.
                        args.Cancel = true;
                        console.WriteLine(ConsoleMessages.CancelRequested);
                        cancellation.Cancel();
                    };

                    Console.CancelKeyPress += cancelHandler;

                    exitCode = await RunUnattendedAsync(parseResult.Options!, console, cancellation.Token)
                        .ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception exception)
        {
            console.WriteError($"Unexpected error: {exception.Message}");
            exitCode = UnattendedExitCode.SomePackagesFailed;
        }
        finally
        {
            if (cancelHandler is not null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }

            console.Dispose();
            _unattendedOutput = null;

            Shutdown((int)exitCode);
        }
    }

    /// <summary>
    /// Lắp ráp đúng các thành phần mà giao diện đang dùng, chỉ khác: không ViewModel, không cửa sổ.
    /// </summary>
    private static async Task<UnattendedExitCode> RunUnattendedAsync(
        CommandLineOptions options,
        IUnattendedOutput output,
        CancellationToken cancellationToken)
    {
        var localizer = new ResourceStringLocalizer();
        var logger = new AppLogger(logDirectory: null, writeToFile: true, localizer);
        var processRunner = new ProcessRunner();
        var wingetService = new WingetService(processRunner, logger);
        var repository = new JsonProfileRepository(dataFilePath: null, logger: logger, localizer: localizer);
        var queueService = new InstallationQueueService(wingetService, logger);

        var runner = new UnattendedRunner(
            wingetService, repository, queueService, localizer, output, logger);

        return await runner.RunAsync(options, cancellationToken).ConfigureAwait(true);
    }
```

Thêm `using WindowsSetupAssistant.App.Cli;` vào đầu file.

- [ ] **Bước 5: Sửa trình xử lý lỗi để không treo máy**

Thay toàn bộ thân `OnDispatcherUnhandledException` bằng:

```csharp
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Ở chế độ không giám sát KHÔNG được hiện hộp thoại: một hộp thoại đứng chờ người bấm
        // sẽ treo máy đang chạy không người trông cho tới khi có ai đó đi ngang qua.
        if (_unattendedOutput is { } output)
        {
            output.WriteError($"Unexpected error: {e.Exception.Message}");
            e.Handled = true;
            Shutdown((int)UnattendedExitCode.SomePackagesFailed);
            return;
        }

        MessageBox.Show(
            $"Ứng dụng gặp lỗi không mong đợi:\n\n{e.Exception.Message}",
            "Lỗi",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
```

- [ ] **Bước 6: Chạy test cho xanh**

```bash
dotnet build && dotnet test tests/WindowsSetupAssistant.App.Tests --filter AppStartupBranchTests
```

Kỳ vọng: 5 test xanh.

- [ ] **Bước 7: Kiểm chứng bằng tay rằng giao diện vẫn mở bình thường**

```bash
dotnet run --project src/WindowsSetupAssistant.App
```

Kỳ vọng: cửa sổ mở lên y như trước. Đóng lại.

- [ ] **Bước 8: Chạy toàn bộ test**

```bash
dotnet test
```

- [ ] **Bước 9: Commit**

```bash
git add src/WindowsSetupAssistant.App/App.xaml.cs tests/WindowsSetupAssistant.App.Tests/Cli
git commit -m "feat(cli): phan nhanh khoi dong giua giao dien va che do khong giam sat"
```

---

## Task 6: Kiểm thử đầu-cuối trên bản publish thật

**Files:**
- Tạo: `tools/ui-smoke-test/Run-UnattendedSmokeTest.ps1`
- Sửa: `tools/ui-smoke-test/README.md`

**Vì sao phải có task này:** `AttachConsole`, `ShutdownMode` và mã thoát trả về hệ điều hành
**không thể** kiểm chứng trong test runner. Ba thứ đó chỉ lộ ra khi chạy đúng file `.exe` đã
publish. Kịch bản này dùng lại `winget.exe` giả sẵn có trong `tools/ui-smoke-test/fake-winget`,
nên **không có phần mềm thật nào được cài**.

- [ ] **Bước 1: Publish bản mới nhất**

```bash
dotnet publish src/WindowsSetupAssistant.App -c Release -r win-x64 -o publish
```

- [ ] **Bước 2: Build winget giả (nếu chưa có)**

```bash
dotnet build tools/ui-smoke-test/fake-winget -c Release
```

- [ ] **Bước 3: Viết kịch bản**

Tạo `tools/ui-smoke-test/Run-UnattendedSmokeTest.ps1`:

```powershell
param(
    [string]$AppExe = "..\..\publish\WindowsSetupAssistant.exe",
    [string]$FakeWingetDir = ".\fake-winget\bin\Release\net8.0"
)
$ErrorActionPreference = 'Stop'

$appExe  = (Resolve-Path $AppExe).Path
$fakeBin = (Resolve-Path $FakeWingetDir).Path
$appDir  = Split-Path $appExe -Parent

$pass = 0; $fail = 0
function Check($label, $ok) {
    if ($ok) { $script:pass++; Write-Host ("  [DAT]   " + $label) }
    else     { $script:fail++; Write-Host ("  [TRUOT] " + $label) }
}

# Dat winget GIA len dau PATH: khong co phan mem that nao duoc cai.
$env:PATH = $fakeBin + ";" + $env:PATH

# Du lieu rieng cho lan chay nay, khong dung vao Data that cua nguoi dung.
$work = Join-Path $env:TEMP ("wsa-unattended-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
$dataDir = Join-Path $appDir "Data"
$backup  = $null
if (Test-Path $dataDir) {
    $backup = Join-Path $work "Data-backup"
    Copy-Item $dataDir $backup -Recurse
}

$catalog = @{
    schemaVersion = 1
    profiles = @(@{
        id = [guid]::NewGuid().ToString()
        name = "Smoke"
        packages = @(
            @{ id = [guid]::NewGuid().ToString(); name = "Alpha"; packageId = "Fake.Alpha"; category = "Utility"; source = "winget"; isSelected = $true;  sortOrder = 0 },
            @{ id = [guid]::NewGuid().ToString(); name = "Beta";  packageId = "Fake.Beta";  category = "Utility"; source = "winget"; isSelected = $true;  sortOrder = 1 },
            @{ id = [guid]::NewGuid().ToString(); name = "Gamma"; packageId = "Fake.Gamma"; category = "Utility"; source = "winget"; isSelected = $false; sortOrder = 2 }
        )
        manualSoftware = @()
    })
}
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$catalog | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $dataDir "software-list.json") -Encoding utf8

function Invoke-App([string[]]$AppArgs) {
    $out = Join-Path $work "stdout.txt"
    $err = Join-Path $work "stderr.txt"
    $p = Start-Process -FilePath $appExe -ArgumentList $AppArgs -NoNewWindow -Wait -PassThru `
                       -RedirectStandardOutput $out -RedirectStandardError $err
    return [pscustomobject]@{
        ExitCode = $p.ExitCode
        StdOut   = (Get-Content $out -Raw -ErrorAction SilentlyContinue)
        StdErr   = (Get-Content $err -Raw -ErrorAction SilentlyContinue)
    }
}

try {
    Write-Host "1. --help"
    $r = Invoke-App @("--help")
    Check "ma thoat 0"                 ($r.ExitCode -eq 0)
    Check "in ra huong dan"            ($r.StdOut -match "--unattended")
    Check "nhac quyen Administrator"   ($r.StdOut -match "Administrator")

    Write-Host "2. Tham so sai"
    $r = Invoke-App @("--unattended", "--existing", "reinstall")
    Check "ma thoat 2"                 ($r.ExitCode -eq 2)

    Write-Host "3. Sai ten cau hinh"
    $r = Invoke-App @("--unattended", "--profile", "Khong Ton Tai")
    Check "ma thoat 2"                 ($r.ExitCode -eq 2)
    Check "liet ke cau hinh dang co"   (($r.StdOut + $r.StdErr) -match "Smoke")

    Write-Host "4. Chay that voi winget gia"
    $report = Join-Path $work "report.json"
    $r = Invoke-App @("--unattended", "--profile", "Smoke", "--report", $report)
    Check "ma thoat 0"                 ($r.ExitCode -eq 0)
    Check "in tien trinh goi 1"        ($r.StdOut -match "\[1/2\]")
    Check "in tien trinh goi 2"        ($r.StdOut -match "\[2/2\]")
    Check "khong cua so nao mo ra"     (-not (Get-Process -Name "WindowsSetupAssistant" -ErrorAction SilentlyContinue))
    Check "co file bao cao"            (Test-Path $report)

    if (Test-Path $report) {
        $json = Get-Content $report -Raw | ConvertFrom-Json
        Check "schemaVersion = 1"      ($json.schemaVersion -eq 1)
        Check "dung ten cau hinh"      ($json.profileName -eq "Smoke")
        Check "exitCode khop"          ($json.exitCode -eq 0)
        Check "chi 2 goi duoc tick"    ($json.counts.total -eq 2)
        Check "khong co goi Gamma"     (-not ($json.packages.packageId -contains "Fake.Gamma"))
    }

    Write-Host "5. Duong dan bao cao hong khong lam doi ma thoat"
    $r = Invoke-App @("--unattended", "--profile", "Smoke", "--report", "Z:\khong-ton-tai\a<b>.json")
    Check "van la ma thoat 0"          ($r.ExitCode -eq 0)
    Check "co canh bao"                (($r.StdOut + $r.StdErr) -match "WARNING")
}
finally {
    if ($backup) {
        Remove-Item $dataDir -Recurse -Force
        Copy-Item $backup $dataDir -Recurse
    }
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host ("Ket qua: " + $pass + " dat, " + $fail + " truot")
if ($fail -gt 0) { exit 1 }
```

- [ ] **Bước 4: Chạy kịch bản**

```powershell
cd tools/ui-smoke-test; .\Run-UnattendedSmokeTest.ps1
```

Kỳ vọng: **18 mục đều [DAT]**, `0 truot`.

Nếu mục "in tien trinh goi 1" trượt trong khi mã thoát đúng, nguyên nhân gần như chắc chắn là
`RebindStandardStreams` ở Task 2 bị bỏ sót hoặc gọi sai thứ tự.

- [ ] **Bước 5: Chạy lại lần thứ hai để chắc chắn ổn định**

```powershell
.\Run-UnattendedSmokeTest.ps1
```

Kỳ vọng: kết quả giống hệt lần đầu.

- [ ] **Bước 6: Ghi thêm một mục vào README của thư mục công cụ**

Thêm vào `tools/ui-smoke-test/README.md` một đoạn nêu: kịch bản thứ hai kiểm tra chế độ không
giám sát, dùng chung `winget.exe` giả, sao lưu và khôi phục thư mục `Data` nên không đụng vào
danh sách thật của người dùng.

- [ ] **Bước 7: Commit**

```bash
git add tools/ui-smoke-test
git commit -m "test(cli): kich ban kiem thu dau-cuoi cho che do khong giam sat"
```

---

## Task 7: Tài liệu

**Files:**
- Sửa: `README.md`
- Sửa: `docs/build-guide.js`
- Sinh lại: `docs/Huong-dan-su-dung-Windows-Setup-Assistant.docx`

- [ ] **Bước 1: Thêm mục vào `README.md`**

Chèn một mục mới sau mục "6. Cách dùng", gồm: bảng năm tham số, bảng năm mã thoát, ví dụ
báo cáo JSON rút gọn, cảnh báo về quyền Administrator, và script mẫu:

````markdown
## 7. Chế độ không giám sát (cài bằng một dòng lệnh)

```powershell
.\WindowsSetupAssistant.exe --unattended --profile "Máy công ty"
```

Không mở cửa sổ nào. Tiến trình in thẳng ra cửa sổ lệnh, kết quả ghi vào
`Reports\unattended-<thời điểm>.json`.

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `--unattended` | — | **Bắt buộc.** Không có nó thì ứng dụng mở giao diện như cũ. |
| `--profile <tên>` | cấu hình đang chọn | Cấu hình cần cài |
| `--existing skip\|upgrade` | `skip` | Gói đã có trên máy: bỏ qua hay nâng cấp |
| `--report <đường dẫn>` | `Reports\unattended-<thời điểm>.json` | Nơi ghi báo cáo |
| `--help` | — | In hướng dẫn |

| Mã thoát | Ý nghĩa |
|---|---|
| `0` | Mọi gói đã xử lý xong |
| `1` | Có ít nhất một gói lỗi — đọc file báo cáo |
| `2` | Sai tham số, hoặc không có cấu hình tên đó |
| `3` | Máy không có WinGet |
| `4` | Bị huỷ bằng Ctrl+C |

> **Quyền Administrator:** chế độ này **không** hiện hộp thoại UAC, vì một hộp thoại đứng chờ
> người bấm sẽ phá hỏng đúng thứ tính năng này sinh ra để làm. Nếu cần cài ở phạm vi toàn máy,
> hãy **mở PowerShell bằng quyền Administrator trước**, rồi mới gõ lệnh.

Script cài cho nhiều máy:

```powershell
$ket_qua = @()
foreach ($may in @("PC-01", "PC-02", "PC-03")) {
    Invoke-Command -ComputerName $may -ScriptBlock {
        & "\\file-server\setup\WindowsSetupAssistant.exe" --unattended --profile "Máy công ty"
        $LASTEXITCODE
    } | ForEach-Object { $ket_qua += [pscustomobject]@{ May = $may; MaThoat = $_ } }
}
$ket_qua | Format-Table
```
````

Nhớ đánh số lại các mục đứng sau.

- [ ] **Bước 2: Thêm một chương vào tài liệu `.docx`**

Mở `docs/build-guide.js`. Các hàm dựng nội dung đã có sẵn: `H1`, `H2`, `P`, `code`, `table`,
`callout`, `bulletR`, `spacer`. Nội dung tài liệu là mảng `children` (khai báo ở dòng 178).

Chèn vào cuối mảng `children`, **trước** dòng `const doc = new Document({`, một chương gồm:

```javascript
children.push(
  H1('Cài hàng loạt bằng một dòng lệnh'),
  P('Khi phải cài cho nhiều máy trong cùng một buổi, bạn không cần mở ứng dụng trên từng máy. ' +
    'Chuẩn bị cấu hình một lần trong giao diện, sau đó chạy một dòng lệnh trên mỗi máy.'),
  callout('QUAN TRỌNG',
    'Mở PowerShell bằng quyền Administrator TRƯỚC khi gõ lệnh. Chế độ này cố ý không hiện hộp ' +
    'thoại UAC, nên nếu thiếu quyền thì một số phần mềm sẽ cài trượt mà không có gì báo cho bạn.',
    WARN),
  code('.\\WindowsSetupAssistant.exe --unattended --profile "Máy công ty"'),
  H2('Các tham số'),
  table([2600, 1800, 4600],
    ['Tham số', 'Mặc định', 'Ý nghĩa'],
    [
      ['--unattended', '(bắt buộc)', 'Bật chế độ. Không có nó thì ứng dụng mở giao diện như cũ.'],
      ['--profile <tên>', 'cấu hình đang chọn', 'Cấu hình cần cài'],
      ['--existing skip|upgrade', 'skip', 'Gói đã có trên máy: bỏ qua hay nâng cấp'],
      ['--report <đường dẫn>', 'thư mục Reports', 'Nơi ghi file báo cáo'],
      ['--help', '', 'In hướng dẫn']
    ]),
  H2('Đọc kết quả'),
  P('Sau khi chạy xong, gõ echo $LASTEXITCODE trong PowerShell để biết kết quả:'),
  table([1400, 7600],
    ['Mã thoát', 'Ý nghĩa'],
    [
      ['0', 'Mọi phần mềm đã xử lý xong'],
      ['1', 'Có ít nhất một phần mềm cài trượt — mở file báo cáo để xem gói nào'],
      ['2', 'Gõ sai tham số, hoặc không có cấu hình tên đó'],
      ['3', 'Máy chưa có WinGet — cài App Installer trước'],
      ['4', 'Bạn đã nhấn Ctrl+C để dừng']
    ]),
  P('File báo cáo JSON nằm trong thư mục Reports cạnh file .exe. Hãy lưu file này lại làm hồ sơ ' +
    'bàn giao máy: nó ghi rõ máy nào, lúc nào, cài những gì, gói nào trượt và vì sao.')
);
```

- [ ] **Bước 3: Sinh lại tài liệu**

```bash
cd docs && node build-guide.js
```

Kỳ vọng: in ra đường dẫn file `.docx` đã ghi, không lỗi.

- [ ] **Bước 4: Mở file `.docx` kiểm tra bằng mắt**

Kiểm: chương mới nằm đúng cuối tài liệu, hai bảng hiển thị đủ cột không tràn lề, khối lệnh
không bị xuống dòng giữa chừng, và hộp cảnh báo màu cam về quyền Administrator hiện rõ.

- [ ] **Bước 5: Commit**

```bash
git add README.md docs/build-guide.js docs/Huong-dan-su-dung-Windows-Setup-Assistant.docx
git commit -m "docs: huong dan che do khong giam sat trong README va tai lieu"
```

---

## Kiểm tra cuối cùng trước khi gộp

- [ ] `dotnet build` — 0 Warning, 0 Error
- [ ] `dotnet test` — toàn bộ xanh
- [ ] `dotnet publish src/WindowsSetupAssistant.App -c Release -r win-x64 -o publish` — ra đúng một file `.exe`
- [ ] `tools/ui-smoke-test/Run-UiSmokeTest.ps1` — giao diện vẫn nguyên vẹn, không hỏng vì Task 5
- [ ] `tools/ui-smoke-test/Run-UnattendedSmokeTest.ps1` — 18/18 đạt
- [ ] Bấm đúp vào `publish/WindowsSetupAssistant.exe` — giao diện mở bình thường
- [ ] `.\publish\WindowsSetupAssistant.exe --help` trong PowerShell — hướng dẫn hiện ra, không có cửa sổ nào
