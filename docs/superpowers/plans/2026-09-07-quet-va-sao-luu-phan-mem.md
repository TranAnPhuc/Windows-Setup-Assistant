# Kế hoạch triển khai: Quét và sao lưu phần mềm

> **Dành cho người/agent thực thi:** BẮT BUỘC dùng skill `superpowers:subagent-driven-development`
> (khuyên dùng) hoặc `superpowers:executing-plans` để làm theo từng task. Các bước dùng cú pháp
> checkbox (`- [ ]`) để theo dõi tiến độ.

**Mục tiêu:** Thêm chức năng quét phần mềm đang có trên máy, cho kỹ thuật viên lọc bớt, rồi lưu
thành một cấu hình cài đặt trong ứng dụng kèm bộ file sao lưu JSON + CSV mang theo USB.

**Kiến trúc:** Bám đúng chiều phụ thuộc sẵn có `App → Infrastructure → Application → Domain`.
Quy tắc phân loại là hàm thuần đặt ở Domain (test không cần mock). Việc gọi winget đi qua
`IWingetService` đã có. Việc ghi file đi qua abstraction mới `IBackupExporter`.

**Công nghệ:** .NET 8, WPF, xUnit. Không thêm gói NuGet nào.

**Spec gốc:** `docs/superpowers/specs/2026-09-07-quet-va-sao-luu-phan-mem-design.md`

## Ràng buộc toàn cục

Mọi task đều phải tuân thủ:

- Không thêm dependency NuGet mới.
- Chú thích và chuỗi hiển thị viết bằng **tiếng Việt**, giọng văn giống code hiện có.
- Không test nào được gọi winget thật, không ghi file ra ngoài thư mục tạm.
- **Toàn bộ test cũ phải tiếp tục xanh.** Không được sửa test cũ để lách. Mốc hiện tại: **145 test**
  (`WindowsSetupAssistant.Tests` 128 + `WindowsSetupAssistant.App.Tests` 17).
- `dotnet build` phải giữ **0 lỗi, 0 cảnh báo**.
- Tiền tố `ARP\` và `MSIX\` so sánh bằng `StringComparison.OrdinalIgnoreCase`.
- Mọi thao tác chạm winget phải `async` + nhận `CancellationToken`.
- Thư mục làm việc cho mọi lệnh: `D:\Desktop\AI\Windows-Setup-Assistant`

## Cấu trúc file

| File | Trách nhiệm | Task |
|---|---|---|
| `src/…Infrastructure/Winget/WingetOutputParser.cs` | *(sửa)* giữ lại dòng `ARP\`/`MSIX\` | 1 |
| `src/…Domain/Enums/InstalledSoftwareKind.cs` | *(mới)* 3 nhóm phân loại | 2 |
| `src/…Domain/Models/InstalledSoftwareEntry.cs` | *(mới)* một phần mềm quét được | 2 |
| `src/…Domain/Models/MachineSnapshot.cs` | *(mới)* kết quả một lần quét | 2 |
| `src/…Domain/Classification/InstalledSoftwareClassifier.cs` | *(mới)* quy tắc phân loại, hàm thuần | 2 |
| `src/…Domain/Entities/InstallationProfile.cs` | *(sửa)* thêm `ManualSoftware` | 3 |
| `src/…Infrastructure/Persistence/CatalogNormalizer.cs` | *(sửa)* chống null cho `ManualSoftware` | 3 |
| `src/…Application/Abstractions/IMachineScanService.cs` | *(mới)* cổng quét máy | 4 |
| `src/…Application/Services/MachineScanService.cs` | *(mới)* điều phối quét + phân loại | 4 |
| `src/…Application/Models/BackupPaths.cs` | *(mới)* đường dẫn 2 file đã ghi | 5 |
| `src/…Application/Abstractions/IBackupExporter.cs` | *(mới)* cổng ghi file sao lưu | 5 |
| `src/…Infrastructure/Persistence/BackupExporter.cs` | *(mới)* ghi JSON + CSV | 5 |
| `src/…Domain/Classification/SoftwareCategoryGuesser.cs` | *(mới)* đoán nhóm phần mềm, dùng chung | 6 |
| `src/…App/ViewModels/ScanResultViewModel.cs` | *(mới)* trạng thái cửa sổ kết quả quét | 7 |
| `src/…App/Views/ScanResultWindow.xaml(.cs)` | *(mới)* giao diện xem lại và tick chọn | 8 |
| `src/…App/ViewModels/MainViewModel.cs` | *(sửa)* lệnh `ScanAndBackupCommand` | 6, 8 |

---

## Task 1: Parser giữ lại dòng Apps & Features và MSIX

**Files:**
- Modify: `src/WindowsSetupAssistant.Infrastructure/Winget/WingetOutputParser.cs` (hàm `LooksLikePackageId`)
- Test: `tests/WindowsSetupAssistant.Tests/Winget/WingetOutputParserTests.cs`

**Interfaces:**
- Consumes: không có
- Produces: `WingetOutputParser.ParseTable` từ nay trả thêm dòng có `PackageId` bắt đầu bằng
  `ARP\` hoặc `MSIX\`. Task 2 và 4 dựa vào điều này.

- [ ] **Bước 1: Viết test thất bại**

Thêm vào cuối class `WingetOutputParserTests`:

```csharp
    // Output thật của: winget list (trích 3 dòng đại diện cho 3 loại mục)
    private const string MixedListOutput =
        "Name                     Id                                              Version   Source\n" +
        "-----------------------------------------------------------------------------------------\n" +
        "7-Zip 17.00 beta (x64)   7zip.7zip                                       17.00     winget\n" +
        "Android Studio           ARP\\Machine\\X64\\Android Studio                  2026.1          \n" +
        "3D Viewer                MSIX\\Microsoft.Microsoft3DViewer_7.2602.8012.0  7.2602.8012.0   \n";

    [Fact]
    public void ParseTable_KeepsAppsAndFeaturesRow()
    {
        var packages = WingetOutputParser.ParseTable(MixedListOutput);

        var entry = Assert.Single(packages, p => p.Name == "Android Studio");
        Assert.Equal(@"ARP\Machine\X64\Android Studio", entry.PackageId);
        Assert.Equal("2026.1", entry.Version);
    }

    [Fact]
    public void ParseTable_KeepsMsixRow()
    {
        var packages = WingetOutputParser.ParseTable(MixedListOutput);

        Assert.Contains(packages, p => p.PackageId.StartsWith(@"MSIX\", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseTable_StillReadsNormalWingetRow()
    {
        var packages = WingetOutputParser.ParseTable(MixedListOutput);

        var sevenZip = Assert.Single(packages, p => p.PackageId == "7zip.7zip");
        Assert.Equal("17.00", sevenZip.Version);
        Assert.Equal("winget", sevenZip.Source);
    }

    [Fact]
    public void ParseTable_SummaryLineIsStillRejected()
    {
        // Dong tong ket khong duoc bien thanh mot goi - day la ly do bo loc ton tai.
        var output = MixedListOutput + "2 upgrades available.\n";

        var packages = WingetOutputParser.ParseTable(output);

        Assert.Equal(3, packages.Count);
        Assert.DoesNotContain(packages, p => p.Name.Contains("upgrades available"));
    }
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~ParseTable_Keeps"
```

Kết quả mong đợi: **FAIL** — `ParseTable_KeepsAppsAndFeaturesRow` và `ParseTable_KeepsMsixRow`
báo `Assert.Single() Failure: The collection was empty` vì hai dòng đó đang bị loại.

- [ ] **Bước 3: Sửa `LooksLikePackageId`**

Thay toàn bộ hàm hiện có bằng:

```csharp
    private static bool LooksLikePackageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Mục trong Apps & Features và app MSIX có Id chứa khoảng trắng
        // (ví dụ "ARP\Machine\X64\Android Studio") nhưng vẫn là dòng dữ liệu thật.
        // Đây là tiền tố nội bộ của winget, không bị dịch theo ngôn ngữ Windows.
        if (value.StartsWith(@"ARP\", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith(@"MSIX\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return char.IsLetterOrDigit(value[0]) || value[0] == '{';
    }
```

- [ ] **Bước 4: Chạy lại 4 test mới**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~ParseTable_"
```

Kết quả mong đợi: **PASS**, toàn bộ.

- [ ] **Bước 5: Chạy toàn bộ test cũ để chắc chắn không vỡ gì**

```bash
dotnet test
```

Kết quả mong đợi: **149 test pass** (145 cũ + 4 mới), 0 fail.
Nếu có test cũ đỏ: **dừng lại và sửa code, tuyệt đối không sửa test cũ.**

- [ ] **Bước 6: Commit**

```bash
git add src/WindowsSetupAssistant.Infrastructure/Winget/WingetOutputParser.cs tests/WindowsSetupAssistant.Tests/Winget/WingetOutputParserTests.cs
git commit -m "Parser giu lai dong Apps & Features va MSIX khi doc winget list"
```

---

## Task 2: Model và quy tắc phân loại ở Domain

**Files:**
- Create: `src/WindowsSetupAssistant.Domain/Enums/InstalledSoftwareKind.cs`
- Create: `src/WindowsSetupAssistant.Domain/Models/InstalledSoftwareEntry.cs`
- Create: `src/WindowsSetupAssistant.Domain/Models/MachineSnapshot.cs`
- Create: `src/WindowsSetupAssistant.Domain/Classification/InstalledSoftwareClassifier.cs`
- Test: `tests/WindowsSetupAssistant.Tests/Domain/InstalledSoftwareClassifierTests.cs`

**Interfaces:**
- Consumes: `WingetPackageInfo(Name, PackageId, Version, AvailableVersion, Source)` từ
  `WindowsSetupAssistant.Domain.Models`; `PackageIdValidator.IsValid(string?)`
- Produces:
  - `enum InstalledSoftwareKind { WingetPackage, ManualOnly, SystemComponent }`
  - `record InstalledSoftwareEntry(string Name, string RawId, string Version, string? Source, InstalledSoftwareKind Kind)`
  - `MachineSnapshot { MachineName, ScannedAt, Entries, WingetPackages, ManualOnly, SystemComponents }`
  - `InstalledSoftwareClassifier.Classify(WingetPackageInfo) → InstalledSoftwareKind`
  - `InstalledSoftwareClassifier.ToEntry(WingetPackageInfo) → InstalledSoftwareEntry`

- [ ] **Bước 1: Viết test thất bại**

Tạo `tests/WindowsSetupAssistant.Tests/Domain/InstalledSoftwareClassifierTests.cs`:

```csharp
using WindowsSetupAssistant.Domain.Classification;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Tests.Domain;

public class InstalledSoftwareClassifierTests
{
    private static WingetPackageInfo Row(string name, string id, string? source) =>
        new(name, id, "1.0", null, source);

    [Theory]
    [InlineData("7zip.7zip", "winget")]
    [InlineData("VNGCorp.Zalo", "winget")]
    [InlineData("9WZDNCRFHVJL", "msstore")]
    [InlineData("Notepad++.Notepad++", "WinGet")]
    public void Classify_WingetSourceWithValidId_IsWingetPackage(string id, string source)
    {
        Assert.Equal(InstalledSoftwareKind.WingetPackage,
            InstalledSoftwareClassifier.Classify(Row("Ten", id, source)));
    }

    [Fact]
    public void Classify_MsixPrefix_IsSystemComponent()
    {
        var row = Row("3D Viewer", @"MSIX\Microsoft.Microsoft3DViewer_7.2602.8012.0", null);

        Assert.Equal(InstalledSoftwareKind.SystemComponent, InstalledSoftwareClassifier.Classify(row));
    }

    [Fact]
    public void Classify_MsixPrefixWinsOverWingetSource()
    {
        // Tien to MSIX duoc xet TRUOC, du dong do co nguon winget.
        var row = Row("App he thong", @"MSIX\Microsoft.Something_1.0", "winget");

        Assert.Equal(InstalledSoftwareKind.SystemComponent, InstalledSoftwareClassifier.Classify(row));
    }

    [Fact]
    public void Classify_AppsAndFeaturesEntry_IsManualOnly()
    {
        var row = Row("Android Studio", @"ARP\Machine\X64\Android Studio", null);

        Assert.Equal(InstalledSoftwareKind.ManualOnly, InstalledSoftwareClassifier.Classify(row));
    }

    [Fact]
    public void Classify_NoSource_IsManualOnly()
    {
        Assert.Equal(InstalledSoftwareKind.ManualOnly,
            InstalledSoftwareClassifier.Classify(Row("Phan mem noi bo", "NoiBo.KeToan", null)));
    }

    [Fact]
    public void Classify_UnknownSource_IsManualOnly()
    {
        Assert.Equal(InstalledSoftwareKind.ManualOnly,
            InstalledSoftwareClassifier.Classify(Row("La", "Nguon.La", "nguonla")));
    }

    [Fact]
    public void ToEntry_CopiesEveryFieldAndSetsKind()
    {
        var row = new WingetPackageInfo("Git", "Git.Git", "2.55.0", null, "winget");

        var entry = InstalledSoftwareClassifier.ToEntry(row);

        Assert.Equal("Git", entry.Name);
        Assert.Equal("Git.Git", entry.RawId);
        Assert.Equal("2.55.0", entry.Version);
        Assert.Equal("winget", entry.Source);
        Assert.Equal(InstalledSoftwareKind.WingetPackage, entry.Kind);
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~InstalledSoftwareClassifier"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: The type or namespace name 'InstalledSoftwareClassifier' could not be found`.

- [ ] **Bước 3: Tạo enum**

`src/WindowsSetupAssistant.Domain/Enums/InstalledSoftwareKind.cs`:

```csharp
namespace WindowsSetupAssistant.Domain.Enums;

/// <summary>
/// Phân loại một phần mềm quét được trên máy, quyết định nó đi vào nhóm nào của bản sao lưu.
/// </summary>
public enum InstalledSoftwareKind
{
    /// <summary>Có trên kho WinGet nên cài lại tự động được.</summary>
    WingetPackage = 0,

    /// <summary>Không có trên kho WinGet - phải cài tay, chỉ ghi lại để khỏi bỏ sót.</summary>
    ManualOnly = 1,

    /// <summary>App hệ thống của Windows - mặc định không đưa vào bản sao lưu.</summary>
    SystemComponent = 2
}
```

- [ ] **Bước 4: Tạo model**

`src/WindowsSetupAssistant.Domain/Models/InstalledSoftwareEntry.cs`:

```csharp
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Models;

/// <summary>
/// Một phần mềm phát hiện được trên máy khi quét.
/// </summary>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="RawId">
/// Id nguyên gốc từ winget list. Với nhóm cài tay đây KHÔNG phải Package Id hợp lệ,
/// ví dụ "ARP\Machine\X64\Android Studio" - giữ nguyên để kỹ thuật viên nhận ra phần mềm.
/// </param>
/// <param name="Version">Phiên bản đang cài.</param>
/// <param name="Source">"winget", "msstore" hoặc null.</param>
/// <param name="Kind">Nhóm phân loại.</param>
public sealed record InstalledSoftwareEntry(
    string Name,
    string RawId,
    string Version,
    string? Source,
    InstalledSoftwareKind Kind);
```

`src/WindowsSetupAssistant.Domain/Models/MachineSnapshot.cs`:

```csharp
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Models;

/// <summary>Kết quả một lần quét phần mềm trên máy.</summary>
public sealed class MachineSnapshot
{
    public required string MachineName { get; init; }

    public required DateTimeOffset ScannedAt { get; init; }

    public required IReadOnlyList<InstalledSoftwareEntry> Entries { get; init; }

    public IEnumerable<InstalledSoftwareEntry> WingetPackages =>
        Entries.Where(e => e.Kind == InstalledSoftwareKind.WingetPackage);

    public IEnumerable<InstalledSoftwareEntry> ManualOnly =>
        Entries.Where(e => e.Kind == InstalledSoftwareKind.ManualOnly);

    public IEnumerable<InstalledSoftwareEntry> SystemComponents =>
        Entries.Where(e => e.Kind == InstalledSoftwareKind.SystemComponent);
}
```

- [ ] **Bước 5: Tạo classifier**

`src/WindowsSetupAssistant.Domain/Classification/InstalledSoftwareClassifier.cs`:

```csharp
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.Domain.Classification;

/// <summary>
/// Quy tắc xếp một dòng "winget list" vào nhóm nào.
/// Hàm thuần, không phụ thuộc gì - test được mà không cần mock.
/// </summary>
public static class InstalledSoftwareClassifier
{
    private const string MsixPrefix = @"MSIX\";

    /// <summary>Các nguồn mà WinGet cài lại được.</summary>
    private static readonly string[] ReinstallableSources = { "winget", "msstore" };

    public static InstalledSoftwareKind Classify(WingetPackageInfo row)
    {
        ArgumentNullException.ThrowIfNull(row);

        // Xét TRƯỚC: app hệ thống thì không đưa vào bản sao lưu dù nguồn là gì.
        if (row.PackageId.StartsWith(MsixPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return InstalledSoftwareKind.SystemComponent;
        }

        var isReinstallable = row.Source is not null
            && ReinstallableSources.Contains(row.Source.Trim(), StringComparer.OrdinalIgnoreCase);

        return isReinstallable && PackageIdValidator.IsValid(row.PackageId)
            ? InstalledSoftwareKind.WingetPackage
            : InstalledSoftwareKind.ManualOnly;
    }

    public static InstalledSoftwareEntry ToEntry(WingetPackageInfo row) =>
        new(row.Name, row.PackageId, row.Version, row.Source, Classify(row));
}
```

- [ ] **Bước 6: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~InstalledSoftwareClassifier"
```

Kết quả mong đợi: **10 test PASS**.

- [ ] **Bước 7: Commit**

```bash
git add src/WindowsSetupAssistant.Domain tests/WindowsSetupAssistant.Tests/Domain/InstalledSoftwareClassifierTests.cs
git commit -m "Them model va quy tac phan loai phan mem quet duoc tren may"
```

---

## Task 3: Cấu hình mang theo danh sách phần mềm cài tay

**Files:**
- Modify: `src/WindowsSetupAssistant.Domain/Entities/InstallationProfile.cs`
- Modify: `src/WindowsSetupAssistant.Infrastructure/Persistence/CatalogNormalizer.cs` (dòng 47, cạnh `profile.Packages ??= …`)
- Test: `tests/WindowsSetupAssistant.Tests/Persistence/JsonProfileRepositoryTests.cs`
- Test: `tests/WindowsSetupAssistant.Tests/Persistence/CatalogNormalizerTests.cs`

**Interfaces:**
- Consumes: `InstalledSoftwareEntry` (Task 2)
- Produces: `InstallationProfile.ManualSoftware` kiểu `List<InstalledSoftwareEntry>`, mặc định rỗng,
  được `Clone()` và lưu/đọc JSON giữ nguyên. Task 5 và 7 dựa vào.

- [ ] **Bước 1: Viết test thất bại**

Thêm vào `JsonProfileRepositoryTests`:

```csharp
    [Fact]
    public async Task SaveThenLoad_KeepsManualSoftwareList()
    {
        var repository = CreateRepository();

        var profile = new InstallationProfile
        {
            Name = "Sao lưu máy cũ",
            Packages = new List<SoftwarePackage>
            {
                new() { Name = "Git", PackageId = "Git.Git", Category = SoftwareCategory.Development }
            },
            ManualSoftware = new List<InstalledSoftwareEntry>
            {
                new("Android Studio", @"ARP\Machine\X64\Android Studio", "2026.1", null,
                    InstalledSoftwareKind.ManualOnly),
                new("Phần mềm kế toán MISA", @"ARP\Machine\X64\MISA SME", "2024", null,
                    InstalledSoftwareKind.ManualOnly)
            }
        };

        var catalog = new SoftwareCatalog
        {
            ActiveProfileId = profile.Id,
            Profiles = new List<InstallationProfile> { profile }
        };

        await repository.SaveAsync(catalog);
        var loaded = await CreateRepository().LoadAsync();

        var loadedProfile = Assert.Single(loaded.Profiles);
        Assert.Equal(2, loadedProfile.ManualSoftware.Count);
        Assert.Equal("Android Studio", loadedProfile.ManualSoftware[0].Name);
        Assert.Equal(@"ARP\Machine\X64\Android Studio", loadedProfile.ManualSoftware[0].RawId);
        Assert.Equal("Phần mềm kế toán MISA", loadedProfile.ManualSoftware[1].Name);

        // Muc cai tay co RawId khong hop le - KHONG duoc bi PackageIdValidator loai bo.
        Assert.Single(loadedProfile.Packages);
    }

    [Fact]
    public async Task LoadAsync_OldFileWithoutManualSoftware_GivesEmptyList()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);

        var json = """
        {
          "schemaVersion": 1,
          "activeProfileId": "11111111-1111-1111-1111-111111111111",
          "profiles": [
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "name": "File cũ",
              "packages": [
                { "name": "Chrome", "packageId": "Google.Chrome", "category": "Browser" }
              ]
            }
          ]
        }
        """;

        await File.WriteAllTextAsync(_dataFile, json);

        var catalog = await CreateRepository().LoadAsync();

        Assert.NotNull(catalog.Profiles[0].ManualSoftware);
        Assert.Empty(catalog.Profiles[0].ManualSoftware);
    }
```

Thêm `using WindowsSetupAssistant.Domain.Enums;` và `using WindowsSetupAssistant.Domain.Models;`
vào đầu file nếu chưa có.

Thêm vào `CatalogNormalizerTests`:

```csharp
    [Fact]
    public void Normalize_NullManualSoftware_BecomesEmptyList()
    {
        var profile = new InstallationProfile { Name = "A" };
        profile.ManualSoftware = null!;

        var catalog = new SoftwareCatalog { Profiles = new List<InstallationProfile> { profile } };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);

        Assert.NotNull(normalized.Profiles[0].ManualSoftware);
        Assert.Empty(normalized.Profiles[0].ManualSoftware);
    }

    [Fact]
    public void Normalize_ManualSoftwareIsNotDroppedByPackageIdValidation()
    {
        var profile = new InstallationProfile
        {
            Name = "A",
            ManualSoftware = new List<InstalledSoftwareEntry>
            {
                new("Android Studio", @"ARP\Machine\X64\Android Studio", "2026.1", null,
                    InstalledSoftwareKind.ManualOnly)
            }
        };

        var catalog = new SoftwareCatalog { Profiles = new List<InstallationProfile> { profile } };

        var normalized = CatalogNormalizer.Normalize(catalog, out var removed);

        Assert.Single(normalized.Profiles[0].ManualSoftware);
        Assert.Empty(removed);
    }
```

Thêm `using WindowsSetupAssistant.Domain.Enums;` và `using WindowsSetupAssistant.Domain.Models;` nếu chưa có.

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~ManualSoftware"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0117: 'InstallationProfile' does not contain a definition for 'ManualSoftware'`.

- [ ] **Bước 3: Thêm thuộc tính vào `InstallationProfile`**

Thêm sau thuộc tính `Packages`:

```csharp
    /// <summary>
    /// Phần mềm không cài lại tự động được, chỉ ghi lại để kỹ thuật viên cài tay.
    /// Danh sách này KHÔNG đi qua PackageIdValidator vì RawId cố tình không phải Package Id hợp lệ.
    /// File JSON cũ không có trường này vẫn đọc bình thường, khi đó danh sách rỗng.
    /// </summary>
    public List<InstalledSoftwareEntry> ManualSoftware { get; set; } = new();
```

Bổ sung `using WindowsSetupAssistant.Domain.Models;` ở đầu file, và thêm dòng sau vào `Clone()`
(ngay sau `Packages = …`):

```csharp
        ManualSoftware = ManualSoftware.ToList()
```

`InstalledSoftwareEntry` là record bất biến nên copy nông danh sách là đủ.

- [ ] **Bước 4: Chống null trong `CatalogNormalizer`**

Ngay sau dòng `profile.Packages ??= new List<SoftwarePackage>();` thêm:

```csharp
            profile.ManualSoftware ??= new List<InstalledSoftwareEntry>();
```

Bổ sung `using WindowsSetupAssistant.Domain.Models;` ở đầu file nếu chưa có.

- [ ] **Bước 5: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~ManualSoftware"
```

Kết quả mong đợi: **4 test PASS**.

- [ ] **Bước 6: Chạy toàn bộ**

```bash
dotnet test
```

Kết quả mong đợi: **163 test pass**, 0 fail.

- [ ] **Bước 7: Commit**

```bash
git add src/WindowsSetupAssistant.Domain/Entities/InstallationProfile.cs src/WindowsSetupAssistant.Infrastructure/Persistence/CatalogNormalizer.cs tests/WindowsSetupAssistant.Tests/Persistence
git commit -m "Cau hinh mang theo danh sach phan mem phai cai tay"
```

---

## Task 4: Dịch vụ quét máy

**Files:**
- Create: `src/WindowsSetupAssistant.Application/Abstractions/IMachineScanService.cs`
- Create: `src/WindowsSetupAssistant.Application/Services/MachineScanService.cs`
- Modify: `tests/WindowsSetupAssistant.Tests/Fakes/FakeWingetService.cs`
- Test: `tests/WindowsSetupAssistant.Tests/Services/MachineScanServiceTests.cs`

**Interfaces:**
- Consumes: `IWingetService.GetInstalledPackagesAsync(CancellationToken)`, `IAppLogger`,
  `InstalledSoftwareClassifier.ToEntry` (Task 2)
- Produces: `IMachineScanService.ScanAsync(CancellationToken) → Task<MachineSnapshot>`. Task 8 dùng.

- [ ] **Bước 1: Mở rộng `FakeWingetService` để trả về dòng tuỳ ý**

Thêm thuộc tính vào `FakeWingetService`:

```csharp
    /// <summary>
    /// Dòng thô trả về cho GetInstalledPackagesAsync. Nếu để rỗng thì rơi về hành vi cũ
    /// (dựng từ InstalledPackageIds) để không làm hỏng các test đã có.
    /// </summary>
    public List<WingetPackageInfo> InstalledRows { get; } = new();
```

Thay thân `GetInstalledPackagesAsync` bằng:

```csharp
    public Task<IReadOnlyList<WingetPackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (InstalledRows.Count > 0)
        {
            return Task.FromResult<IReadOnlyList<WingetPackageInfo>>(InstalledRows.ToList());
        }

        return Task.FromResult<IReadOnlyList<WingetPackageInfo>>(
            InstalledPackageIds.Select(id => new WingetPackageInfo(id, id, "1.0", null, "winget")).ToList());
    }
```

Thêm thuộc tính cho phép mô phỏng lỗi:

```csharp
    public Exception? ThrowOnGetInstalledPackages { get; set; }
```

và ném nó ở đầu hàm trên, ngay sau `ThrowIfCancellationRequested`:

```csharp
        if (ThrowOnGetInstalledPackages is not null)
        {
            throw ThrowOnGetInstalledPackages;
        }
```

- [ ] **Bước 2: Viết test thất bại**

Tạo `tests/WindowsSetupAssistant.Tests/Services/MachineScanServiceTests.cs`:

```csharp
using WindowsSetupAssistant.Application.Services;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.Tests.Services;

public class MachineScanServiceTests
{
    private readonly FakeWingetService _winget = new();
    private readonly RecordingLogger _logger = new();

    private MachineScanService CreateService() => new(_winget, _logger);

    private void GivenInstalled(params WingetPackageInfo[] rows) => _winget.InstalledRows.AddRange(rows);

    [Fact]
    public async Task ScanAsync_SplitsRowsIntoThreeGroups()
    {
        GivenInstalled(
            new WingetPackageInfo("Git", "Git.Git", "2.55.0", null, "winget"),
            new WingetPackageInfo("Android Studio", @"ARP\Machine\X64\Android Studio", "2026.1", null, null),
            new WingetPackageInfo("3D Viewer", @"MSIX\Microsoft.Microsoft3DViewer_7.2", "7.2", null, null));

        var snapshot = await CreateService().ScanAsync();

        Assert.Equal(3, snapshot.Entries.Count);
        Assert.Equal("Git.Git", Assert.Single(snapshot.WingetPackages).RawId);
        Assert.Equal("Android Studio", Assert.Single(snapshot.ManualOnly).Name);
        Assert.Single(snapshot.SystemComponents);
    }

    [Fact]
    public async Task ScanAsync_FillsMachineNameAndTimestamp()
    {
        GivenInstalled(new WingetPackageInfo("Git", "Git.Git", "2.55.0", null, "winget"));

        var before = DateTimeOffset.Now;
        var snapshot = await CreateService().ScanAsync();

        Assert.Equal(Environment.MachineName, snapshot.MachineName);
        Assert.True(snapshot.ScannedAt >= before);
    }

    [Fact]
    public async Task ScanAsync_NothingInstalled_ReturnsEmptySnapshot()
    {
        var snapshot = await CreateService().ScanAsync();

        Assert.Empty(snapshot.Entries);
        Assert.Equal(Environment.MachineName, snapshot.MachineName);
    }

    [Fact]
    public async Task ScanAsync_WingetFails_LetsExceptionThrough()
    {
        _winget.ThrowOnGetInstalledPackages = new InvalidOperationException("winget lỗi");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().ScanAsync());

        Assert.Equal("winget lỗi", error.Message);
        Assert.Contains(_logger.Errors, e => e.Message.Contains("Quét máy thất bại"));
    }

    [Fact]
    public async Task ScanAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().ScanAsync(cts.Token));
    }

    [Fact]
    public async Task ScanAsync_LogsSummary()
    {
        GivenInstalled(
            new WingetPackageInfo("Git", "Git.Git", "2.55.0", null, "winget"),
            new WingetPackageInfo("Android Studio", @"ARP\Machine\X64\Android Studio", "2026.1", null, null));

        await CreateService().ScanAsync();

        Assert.Contains(_logger.Entries, e => e.Message.Contains("Quét xong"));
    }
}
```

- [ ] **Bước 3: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~MachineScanService"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'MachineScanService' could not be found`.

- [ ] **Bước 4: Tạo interface**

`src/WindowsSetupAssistant.Application/Abstractions/IMachineScanService.cs`:

```csharp
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Quét toàn bộ phần mềm đang có trên máy và phân loại chúng,
/// phục vụ việc sao lưu trước khi cài lại Windows.
/// </summary>
public interface IMachineScanService
{
    Task<MachineSnapshot> ScanAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Bước 5: Tạo dịch vụ**

`src/WindowsSetupAssistant.Application/Services/MachineScanService.cs`:

```csharp
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Classification;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.Application.Services;

/// <summary>
/// Gọi WinGet lấy danh sách phần mềm đang cài rồi xếp từng dòng vào nhóm tương ứng.
/// Lớp này không biết winget.exe hoạt động thế nào - việc đó là của IWingetService.
/// </summary>
public sealed class MachineScanService : IMachineScanService
{
    private readonly IWingetService _wingetService;
    private readonly IAppLogger _logger;

    public MachineScanService(IWingetService wingetService, IAppLogger logger)
    {
        _wingetService = wingetService ?? throw new ArgumentNullException(nameof(wingetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MachineSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        _logger.Information("Bắt đầu quét phần mềm đang có trên máy.");

        IReadOnlyList<WingetPackageInfo> rows;

        try
        {
            rows = await _wingetService.GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Quét máy thất bại: {ex.Message}", details: ex.ToString());
            throw;
        }

        var entries = rows.Select(InstalledSoftwareClassifier.ToEntry).ToList();

        var snapshot = new MachineSnapshot
        {
            MachineName = Environment.MachineName,
            ScannedAt = DateTimeOffset.Now,
            Entries = entries
        };

        _logger.Information(
            $"Quét xong: {entries.Count} phần mềm - " +
            $"{snapshot.WingetPackages.Count()} cài lại tự động được, " +
            $"{snapshot.ManualOnly.Count()} phải cài tay, " +
            $"{snapshot.SystemComponents.Count()} app hệ thống.");

        return snapshot;
    }
}
```

- [ ] **Bước 6: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~MachineScanService"
```

Kết quả mong đợi: **6 test PASS**.

- [ ] **Bước 7: Chạy toàn bộ và commit**

```bash
dotnet test
git add src/WindowsSetupAssistant.Application tests/WindowsSetupAssistant.Tests
git commit -m "Them dich vu quet phan mem dang co tren may"
```

Kết quả mong đợi trước khi commit: **169 test pass**.

---

## Task 5: Ghi file sao lưu JSON và CSV

**Files:**
- Create: `src/WindowsSetupAssistant.Application/Models/BackupPaths.cs`
- Create: `src/WindowsSetupAssistant.Application/Abstractions/IBackupExporter.cs`
- Create: `src/WindowsSetupAssistant.Infrastructure/Persistence/BackupExporter.cs`
- Test: `tests/WindowsSetupAssistant.Tests/Persistence/BackupExporterTests.cs`

**Interfaces:**
- Consumes: `InstallationProfile.ManualSoftware` (Task 3), `CatalogJson.Options`
- Produces: `IBackupExporter.ExportAsync(InstallationProfile, string jsonFilePath, int schemaVersion, CancellationToken) → Task<BackupPaths>`;
  `record BackupPaths(string JsonPath, string CsvPath)`. Task 8 dùng.

> **Ghi chú lệch spec:** spec ghi chữ ký không có `schemaVersion`. Thêm tham số này để thực hiện
> đúng yêu cầu "SchemaVersion giữ nguyên giá trị của catalog đang dùng" ở mục 9.1 của spec.

- [ ] **Bước 1: Viết test thất bại**

Tạo `tests/WindowsSetupAssistant.Tests/Persistence/BackupExporterTests.cs`:

```csharp
using System.Text;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;
using WindowsSetupAssistant.Infrastructure.Persistence;

namespace WindowsSetupAssistant.Tests.Persistence;

public class BackupExporterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "wsa-backup-" + Guid.NewGuid().ToString("N"));

    public BackupExporterTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private string JsonPath => Path.Combine(_directory, "sao-luu-MAYTHU-20260907-1030.json");

    private static InstallationProfile CreateProfile() => new()
    {
        Name = "Sao lưu MAYTHU 07-09-2026",
        Packages = new List<SoftwarePackage>
        {
            new() { Name = "Git", PackageId = "Git.Git", Category = SoftwareCategory.Development, SortOrder = 0 }
        },
        ManualSoftware = new List<InstalledSoftwareEntry>
        {
            new("Android Studio", @"ARP\Machine\X64\Android Studio", "2026.1", null, InstalledSoftwareKind.ManualOnly),
            new("Phần mềm, có dấu phẩy", @"ARP\X\A", "1.0", null, InstalledSoftwareKind.ManualOnly),
            new("Tên có \"ngoặc kép\"", @"ARP\X\B", "2.0", null, InstalledSoftwareKind.ManualOnly)
        }
    };

    [Fact]
    public async Task ExportAsync_WritesBothFilesNextToEachOther()
    {
        var paths = await new BackupExporter().ExportAsync(CreateProfile(), JsonPath, 1);

        Assert.True(File.Exists(paths.JsonPath));
        Assert.True(File.Exists(paths.CsvPath));
        Assert.Equal(_directory, Path.GetDirectoryName(paths.CsvPath));
        Assert.EndsWith("-cai-tay.csv", paths.CsvPath);
    }

    [Fact]
    public async Task ExportAsync_JsonCanBeImportedByExistingRepository()
    {
        var paths = await new BackupExporter().ExportAsync(CreateProfile(), JsonPath, 1);

        var repository = new JsonProfileRepository(Path.Combine(_directory, "Data", "software-list.json"));
        var imported = await repository.ImportAsync(paths.JsonPath);

        var profile = Assert.Single(imported.Profiles);
        Assert.Equal("Sao lưu MAYTHU 07-09-2026", profile.Name);
        Assert.Equal(profile.Id, imported.ActiveProfileId);
        Assert.Single(profile.Packages);
        Assert.Equal(3, profile.ManualSoftware.Count);
    }

    [Fact]
    public async Task ExportAsync_CsvStartsWithBomAndSeparatorHint()
    {
        var paths = await new BackupExporter().ExportAsync(CreateProfile(), JsonPath, 1);

        var bytes = await File.ReadAllBytesAsync(paths.CsvPath);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());

        var lines = await File.ReadAllLinesAsync(paths.CsvPath, Encoding.UTF8);
        Assert.Equal("sep=,", lines[0]);
        Assert.Equal("STT,Tên phần mềm,Phiên bản,Mã định danh,Đã cài lại (x)", lines[1]);
    }

    [Fact]
    public async Task ExportAsync_CsvEscapesCommasAndQuotes()
    {
        var paths = await new BackupExporter().ExportAsync(CreateProfile(), JsonPath, 1);

        var text = await File.ReadAllTextAsync(paths.CsvPath, Encoding.UTF8);

        Assert.Contains("\"Phần mềm, có dấu phẩy\"", text);
        Assert.Contains("\"Tên có \"\"ngoặc kép\"\"\"", text);
    }

    [Fact]
    public async Task ExportAsync_CsvNumbersRowsFromOne()
    {
        var paths = await new BackupExporter().ExportAsync(CreateProfile(), JsonPath, 1);

        var lines = await File.ReadAllLinesAsync(paths.CsvPath, Encoding.UTF8);

        Assert.StartsWith("1,Android Studio,2026.1,", lines[2]);
        Assert.StartsWith("2,", lines[3]);
        Assert.StartsWith("3,", lines[4]);
    }

    [Fact]
    public async Task ExportAsync_NoManualSoftware_StillWritesCsvWithHeaderOnly()
    {
        var profile = CreateProfile();
        profile.ManualSoftware.Clear();

        var paths = await new BackupExporter().ExportAsync(profile, JsonPath, 1);

        var lines = await File.ReadAllLinesAsync(paths.CsvPath, Encoding.UTF8);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task ExportAsync_CreatesMissingDirectory()
    {
        var nested = Path.Combine(_directory, "chua-ton-tai", "sao-luu.json");

        var paths = await new BackupExporter().ExportAsync(CreateProfile(), nested, 1);

        Assert.True(File.Exists(paths.JsonPath));
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~BackupExporter"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'BackupExporter' could not be found`.

- [ ] **Bước 3: Tạo model và interface**

`src/WindowsSetupAssistant.Application/Models/BackupPaths.cs`:

```csharp
namespace WindowsSetupAssistant.Application.Models;

/// <summary>Đường dẫn hai file đã ghi khi sao lưu.</summary>
public sealed record BackupPaths(string JsonPath, string CsvPath);
```

`src/WindowsSetupAssistant.Application/Abstractions/IBackupExporter.cs`:

```csharp
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Ghi bản sao lưu ra đĩa: một file JSON nhập lại được và một file CSV để đọc/in.
/// </summary>
public interface IBackupExporter
{
    /// <param name="profile">Cấu hình sao lưu, đã gồm cả danh sách cài tay.</param>
    /// <param name="jsonFilePath">Đường dẫn file JSON; file CSV được đặt cạnh nó.</param>
    /// <param name="schemaVersion">Giữ nguyên SchemaVersion của catalog đang dùng.</param>
    Task<BackupPaths> ExportAsync(
        InstallationProfile profile,
        string jsonFilePath,
        int schemaVersion = 1,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Bước 4: Tạo `BackupExporter`**

`src/WindowsSetupAssistant.Infrastructure/Persistence/BackupExporter.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Application.Models;
using WindowsSetupAssistant.Domain.Entities;

namespace WindowsSetupAssistant.Infrastructure.Persistence;

/// <summary>
/// Ghi bản sao lưu thành hai file nằm cạnh nhau:
/// - JSON: là một SoftwareCatalog đúng chuẩn nên nút "Nhập JSON" sẵn có khôi phục được ngay.
/// - CSV: danh sách phần mềm phải cài tay, mở bằng Excel hoặc in ra để tick.
/// </summary>
public sealed class BackupExporter : IBackupExporter
{
    private const string CsvSuffix = "-cai-tay.csv";

    public async Task<BackupPaths> ExportAsync(
        InstallationProfile profile,
        string jsonFilePath,
        int schemaVersion = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonFilePath);

        var fullJsonPath = Path.GetFullPath(jsonFilePath);
        var directory = Path.GetDirectoryName(fullJsonPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var catalog = new SoftwareCatalog
        {
            SchemaVersion = schemaVersion,
            ActiveProfileId = profile.Id,
            Profiles = new List<InstallationProfile> { profile }
        };

        var json = JsonSerializer.Serialize(catalog, CatalogJson.Options);
        await File.WriteAllTextAsync(fullJsonPath, json, cancellationToken).ConfigureAwait(false);

        var csvPath = BuildCsvPath(fullJsonPath);
        await File.WriteAllTextAsync(csvPath, BuildCsv(profile), CsvEncoding, cancellationToken)
            .ConfigureAwait(false);

        return new BackupPaths(fullJsonPath, csvPath);
    }

    /// <summary>UTF-8 CÓ BOM - thiếu BOM thì Excel hiển thị sai tiếng Việt.</summary>
    private static Encoding CsvEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    internal static string BuildCsvPath(string jsonFilePath)
    {
        var directory = Path.GetDirectoryName(jsonFilePath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(jsonFilePath);
        return Path.Combine(directory, name + CsvSuffix);
    }

    private static string BuildCsv(InstallationProfile profile)
    {
        var builder = new StringBuilder();

        // Chỉ dẫn cho Excel tách cột đúng trên cả máy dùng dấu phẩy lẫn dấu chấm phẩy.
        builder.AppendLine("sep=,");
        builder.AppendLine("STT,Tên phần mềm,Phiên bản,Mã định danh,Đã cài lại (x)");

        var index = 1;

        foreach (var entry in profile.ManualSoftware)
        {
            builder.AppendLine(string.Join(',',
                index.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(entry.Name),
                EscapeCsv(entry.Version),
                EscapeCsv(entry.RawId),
                string.Empty));

            index++;
        }

        return builder.ToString();
    }

    /// <summary>Escape theo RFC 4180.</summary>
    internal static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;

        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return '"' + text.Replace("\"", "\"\"") + '"';
        }

        return text;
    }
}
```

- [ ] **Bước 5: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~BackupExporter"
```

Kết quả mong đợi: **7 test PASS**.

- [ ] **Bước 6: Chạy toàn bộ và commit**

```bash
dotnet test
git add src/WindowsSetupAssistant.Application src/WindowsSetupAssistant.Infrastructure tests/WindowsSetupAssistant.Tests/Persistence/BackupExporterTests.cs
git commit -m "Ghi ban sao luu ra file JSON va CSV"
```

Kết quả mong đợi trước khi commit: **176 test pass**.

---

## Task 6: Tách hàm đoán nhóm phần mềm thành helper dùng chung

**Files:**
- Create: `src/WindowsSetupAssistant.Domain/Classification/SoftwareCategoryGuesser.cs`
- Modify: `src/WindowsSetupAssistant.App/ViewModels/MainViewModel.cs` (xoá `GuessCategory` private ở khoảng dòng 759-795, đổi chỗ gọi ở khoảng dòng 583)
- Test: `tests/WindowsSetupAssistant.Tests/Domain/SoftwareCategoryGuesserTests.cs`

**Interfaces:**
- Consumes: `SoftwareCategory`
- Produces: `SoftwareCategoryGuesser.Guess(string name, string packageId) → SoftwareCategory`. Task 7 dùng.

- [ ] **Bước 1: Viết test thất bại**

Tạo `tests/WindowsSetupAssistant.Tests/Domain/SoftwareCategoryGuesserTests.cs`:

```csharp
using WindowsSetupAssistant.Domain.Classification;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Tests.Domain;

public class SoftwareCategoryGuesserTests
{
    [Theory]
    [InlineData("Google Chrome", "Google.Chrome", SoftwareCategory.Browser)]
    [InlineData("Mozilla Firefox", "Mozilla.Firefox", SoftwareCategory.Browser)]
    [InlineData("Git", "Git.Git", SoftwareCategory.Development)]
    [InlineData("Visual Studio Code", "Microsoft.VisualStudioCode", SoftwareCategory.Development)]
    [InlineData("LibreOffice", "TheDocumentFoundation.LibreOffice", SoftwareCategory.Office)]
    [InlineData("VLC media player", "VideoLAN.VLC", SoftwareCategory.Entertainment)]
    [InlineData("7-Zip", "7zip.7zip", SoftwareCategory.Utility)]
    [InlineData("Notepad++", "Notepad++.Notepad++", SoftwareCategory.Utility)]
    [InlineData("Phần mềm lạ", "NoiBo.KeToan", SoftwareCategory.Other)]
    public void Guess_ReturnsExpectedCategory(string name, string packageId, SoftwareCategory expected)
    {
        Assert.Equal(expected, SoftwareCategoryGuesser.Guess(name, packageId));
    }

    [Fact]
    public void Guess_IsCaseInsensitive()
    {
        Assert.Equal(SoftwareCategory.Browser, SoftwareCategoryGuesser.Guess("GOOGLE CHROME", "GOOGLE.CHROME"));
    }

    [Fact]
    public void Guess_HandlesNullInput()
    {
        Assert.Equal(SoftwareCategory.Other, SoftwareCategoryGuesser.Guess(null!, null!));
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~SoftwareCategoryGuesser"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'SoftwareCategoryGuesser' could not be found`.

- [ ] **Bước 3: Tạo helper**

`src/WindowsSetupAssistant.Domain/Classification/SoftwareCategoryGuesser.cs` — chép nguyên logic
đang có trong `MainViewModel.GuessCategory`, chỉ đổi đầu vào thành hai chuỗi:

```csharp
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.Domain.Classification;

/// <summary>
/// Đoán nhóm phần mềm từ tên và Package Id. Chỉ là gợi ý ban đầu cho người dùng,
/// đoán sai cũng không sao vì họ sửa lại được trong hộp thoại.
/// </summary>
public static class SoftwareCategoryGuesser
{
    public static SoftwareCategory Guess(string name, string packageId)
    {
        var text = $"{name} {packageId}".ToLowerInvariant();

        if (text.Contains("chrome") || text.Contains("firefox") || text.Contains("edge") ||
            text.Contains("brave") || text.Contains("opera") || text.Contains("browser"))
        {
            return SoftwareCategory.Browser;
        }

        if (text.Contains("visualstudio") || text.Contains("git") || text.Contains("node") ||
            text.Contains("python") || text.Contains("docker") || text.Contains("sdk") ||
            text.Contains("java") || text.Contains("code"))
        {
            return SoftwareCategory.Development;
        }

        if (text.Contains("office") || text.Contains("word") || text.Contains("excel") ||
            text.Contains("adobe.acrobat") || text.Contains("libreoffice") || text.Contains("pdf"))
        {
            return SoftwareCategory.Office;
        }

        if (text.Contains("vlc") || text.Contains("spotify") || text.Contains("steam") ||
            text.Contains("player") || text.Contains("music"))
        {
            return SoftwareCategory.Entertainment;
        }

        if (text.Contains("zip") || text.Contains("notepad") || text.Contains("everything") ||
            text.Contains("rar") || text.Contains("driver") || text.Contains("tool"))
        {
            return SoftwareCategory.Utility;
        }

        return SoftwareCategory.Other;
    }
}
```

- [ ] **Bước 4: Xoá bản trùng trong `MainViewModel`**

Xoá toàn bộ hàm `private static SoftwareCategory GuessCategory(WingetPackageInfo info)`.
Đổi chỗ gọi duy nhất (trong `AddPackageFromSearch`) từ:

```csharp
            Category = GuessCategory(info),
```

thành:

```csharp
            Category = SoftwareCategoryGuesser.Guess(info.Name, info.PackageId),
```

Thêm `using WindowsSetupAssistant.Domain.Classification;` ở đầu file.

- [ ] **Bước 5: Chạy test và build**

```bash
dotnet build
dotnet test
```

Kết quả mong đợi: build **0 lỗi 0 cảnh báo**; **187 test pass**.

- [ ] **Bước 6: Commit**

```bash
git add src/WindowsSetupAssistant.Domain/Classification/SoftwareCategoryGuesser.cs src/WindowsSetupAssistant.App/ViewModels/MainViewModel.cs tests/WindowsSetupAssistant.Tests/Domain/SoftwareCategoryGuesserTests.cs
git commit -m "Tach ham doan nhom phan mem thanh helper dung chung o Domain"
```

---

## Task 7: ViewModel cho cửa sổ kết quả quét

**Files:**
- Create: `src/WindowsSetupAssistant.App/ViewModels/ScanResultViewModel.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/ScanResultViewModelTests.cs`

**Interfaces:**
- Consumes: `MachineSnapshot`, `InstalledSoftwareEntry` (Task 2); `SoftwareCategoryGuesser.Guess` (Task 6);
  `InstallationProfile.ManualSoftware` (Task 3); `ObservableObject` từ `WindowsSetupAssistant.App.Mvvm`
- Produces:
  - `ScanEntryViewModel { Entry, Name, RawId, Version, IsSelected }`
  - `ScanResultViewModel(MachineSnapshot)` với `Headline`, `AutomaticHeader`, `ManualHeader`,
    `AutomaticEntries`, `ManualEntries`, `SystemComponentCount`, `ShowSystemComponents`,
    `HasAnySelected`, `BuildProfile(IEnumerable<string> existingProfileNames) → InstallationProfile`

- [ ] **Bước 1: Viết test thất bại**

Tạo `tests/WindowsSetupAssistant.App.Tests/ScanResultViewModelTests.cs`:

```csharp
using WindowsSetupAssistant.App.ViewModels;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.Tests;

public class ScanResultViewModelTests
{
    private static MachineSnapshot Snapshot(params InstalledSoftwareEntry[] entries) => new()
    {
        MachineName = "MAYTHU",
        ScannedAt = new DateTimeOffset(2026, 9, 7, 10, 30, 0, TimeSpan.FromHours(7)),
        Entries = entries
    };

    private static InstalledSoftwareEntry Winget(string name, string id) =>
        new(name, id, "1.0", "winget", InstalledSoftwareKind.WingetPackage);

    private static InstalledSoftwareEntry Manual(string name) =>
        new(name, @"ARP\Machine\X64\" + name, "1.0", null, InstalledSoftwareKind.ManualOnly);

    private static InstalledSoftwareEntry System(string name) =>
        new(name, @"MSIX\" + name, "1.0", null, InstalledSoftwareKind.SystemComponent);

    [Fact]
    public void Constructor_SplitsGroupsAndTicksEverythingByDefault()
    {
        var vm = new ScanResultViewModel(Snapshot(
            Winget("Git", "Git.Git"), Manual("Android Studio"), System("3D Viewer")));

        Assert.Single(vm.AutomaticEntries);
        Assert.Single(vm.ManualEntries);
        Assert.Equal(1, vm.SystemComponentCount);
        Assert.All(vm.AutomaticEntries, e => Assert.True(e.IsSelected));
        Assert.All(vm.ManualEntries, e => Assert.True(e.IsSelected));
    }

    [Fact]
    public void Headers_ShowCounts()
    {
        var vm = new ScanResultViewModel(Snapshot(Winget("Git", "Git.Git"), Manual("A"), Manual("B")));

        Assert.Equal("Đã quét thấy 3 phần mềm trên MAYTHU", vm.Headline);
        Assert.Equal("Cài lại tự động được (1)", vm.AutomaticHeader);
        Assert.Equal("Phải cài tay (2)", vm.ManualHeader);
    }

    [Fact]
    public void ShowSystemComponents_AddsThemUntickedToManualList()
    {
        var vm = new ScanResultViewModel(Snapshot(Manual("Android Studio"), System("3D Viewer")));
        Assert.Single(vm.ManualEntries);

        vm.ShowSystemComponents = true;

        Assert.Equal(2, vm.ManualEntries.Count);
        var systemEntry = vm.ManualEntries.Single(e => e.Name == "3D Viewer");
        Assert.False(systemEntry.IsSelected);
        Assert.Equal("Phải cài tay (2)", vm.ManualHeader);
    }

    [Fact]
    public void ShowSystemComponents_TurningOffRemovesThemAgain()
    {
        var vm = new ScanResultViewModel(Snapshot(Manual("A"), System("B")));

        vm.ShowSystemComponents = true;
        vm.ShowSystemComponents = false;

        Assert.Single(vm.ManualEntries);
        Assert.Equal("A", vm.ManualEntries[0].Name);
    }

    [Fact]
    public void BuildProfile_UsesMachineNameAndScanDate()
    {
        var vm = new ScanResultViewModel(Snapshot(Winget("Git", "Git.Git")));

        var profile = vm.BuildProfile(Array.Empty<string>());

        Assert.Equal("Sao lưu MAYTHU 07-09-2026", profile.Name);
    }

    [Fact]
    public void BuildProfile_NameCollision_GetsSuffix()
    {
        var vm = new ScanResultViewModel(Snapshot(Winget("Git", "Git.Git")));

        var profile = vm.BuildProfile(new[] { "Sao lưu MAYTHU 07-09-2026", "Sao lưu MAYTHU 07-09-2026 (2)" });

        Assert.Equal("Sao lưu MAYTHU 07-09-2026 (3)", profile.Name);
    }

    [Fact]
    public void BuildProfile_MapsEveryFieldOfSelectedWingetEntries()
    {
        var vm = new ScanResultViewModel(Snapshot(
            new InstalledSoftwareEntry("Google Chrome", "Google.Chrome", "152.0", "winget",
                InstalledSoftwareKind.WingetPackage)));

        var package = Assert.Single(vm.BuildProfile(Array.Empty<string>()).Packages);

        Assert.Equal("Google Chrome", package.Name);
        Assert.Equal("Google.Chrome", package.PackageId);
        Assert.Equal("152.0", package.Version);
        Assert.Equal("winget", package.Source);
        Assert.Equal(SoftwareCategory.Browser, package.Category);
        Assert.True(package.IsSelected);
        Assert.Equal(0, package.SortOrder);
    }

    [Fact]
    public void BuildProfile_UnknownSource_FallsBackToWinget()
    {
        var vm = new ScanResultViewModel(Snapshot(
            new InstalledSoftwareEntry("La", "Nguon.La", "1.0", "nguonla", InstalledSoftwareKind.WingetPackage)));

        Assert.Equal("winget", Assert.Single(vm.BuildProfile(Array.Empty<string>()).Packages).Source);
    }

    [Fact]
    public void BuildProfile_SkipsUntickedEntries()
    {
        var vm = new ScanResultViewModel(Snapshot(
            Winget("Git", "Git.Git"), Winget("Chrome", "Google.Chrome"), Manual("A"), Manual("B")));

        vm.AutomaticEntries[0].IsSelected = false;
        vm.ManualEntries[1].IsSelected = false;

        var profile = vm.BuildProfile(Array.Empty<string>());

        Assert.Equal("Google.Chrome", Assert.Single(profile.Packages).PackageId);
        Assert.Equal("A", Assert.Single(profile.ManualSoftware).Name);
    }

    [Fact]
    public void BuildProfile_RenumbersSortOrder()
    {
        var vm = new ScanResultViewModel(Snapshot(
            Winget("A", "A.A"), Winget("B", "B.B"), Winget("C", "C.C")));

        vm.AutomaticEntries[1].IsSelected = false;

        Assert.Equal(new[] { 0, 1 }, vm.BuildProfile(Array.Empty<string>()).Packages.Select(p => p.SortOrder));
    }

    [Fact]
    public void HasAnySelected_FalseOnlyWhenNothingTicked()
    {
        var vm = new ScanResultViewModel(Snapshot(Winget("Git", "Git.Git"), Manual("A")));
        Assert.True(vm.HasAnySelected);

        vm.AutomaticEntries[0].IsSelected = false;
        Assert.True(vm.HasAnySelected);

        vm.ManualEntries[0].IsSelected = false;
        Assert.False(vm.HasAnySelected);
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~ScanResultViewModel"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'ScanResultViewModel' could not be found`.

- [ ] **Bước 3: Tạo ViewModel**

`src/WindowsSetupAssistant.App/ViewModels/ScanResultViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.Domain.Classification;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>Một dòng trong bảng kết quả quét, kèm ô tick chọn.</summary>
public sealed class ScanEntryViewModel : ObservableObject
{
    private bool _isSelected;

    public ScanEntryViewModel(InstalledSoftwareEntry entry, bool isSelected)
    {
        Entry = entry;
        _isSelected = isSelected;
    }

    public InstalledSoftwareEntry Entry { get; }

    public string Name => Entry.Name;

    public string RawId => Entry.RawId;

    public string Version => Entry.Version;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Trạng thái của cửa sổ xem lại kết quả quét: hai bảng cho tick chọn,
/// và việc dựng cấu hình sao lưu từ những mục đã chọn.
/// </summary>
public sealed class ScanResultViewModel : ObservableObject
{
    /// <summary>Nguồn được phép truyền cho tham số --source của winget.</summary>
    private static readonly string[] AllowedSources = { "winget", "msstore" };

    private readonly MachineSnapshot _snapshot;
    private bool _showSystemComponents;

    public ScanResultViewModel(MachineSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));

        AutomaticEntries = new ObservableCollection<ScanEntryViewModel>(
            snapshot.WingetPackages.Select(e => Track(new ScanEntryViewModel(e, true))));

        ManualEntries = new ObservableCollection<ScanEntryViewModel>(
            snapshot.ManualOnly.Select(e => Track(new ScanEntryViewModel(e, true))));
    }

    public ObservableCollection<ScanEntryViewModel> AutomaticEntries { get; }

    public ObservableCollection<ScanEntryViewModel> ManualEntries { get; }

    public int SystemComponentCount => _snapshot.SystemComponents.Count();

    public string Headline => $"Đã quét thấy {_snapshot.Entries.Count} phần mềm trên {_snapshot.MachineName}";

    public string AutomaticHeader => $"Cài lại tự động được ({AutomaticEntries.Count})";

    public string ManualHeader => $"Phải cài tay ({ManualEntries.Count})";

    public string SystemComponentLabel => $"Hiện cả app hệ thống ({SystemComponentCount})";

    public bool HasAnySelected =>
        AutomaticEntries.Any(e => e.IsSelected) || ManualEntries.Any(e => e.IsSelected);

    /// <summary>Bật lên thì app hệ thống được đưa vào bảng "phải cài tay" nhưng KHÔNG tick sẵn.</summary>
    public bool ShowSystemComponents
    {
        get => _showSystemComponents;
        set
        {
            if (!SetProperty(ref _showSystemComponents, value))
            {
                return;
            }

            if (value)
            {
                foreach (var entry in _snapshot.SystemComponents)
                {
                    ManualEntries.Add(Track(new ScanEntryViewModel(entry, false)));
                }
            }
            else
            {
                foreach (var item in ManualEntries
                             .Where(e => e.Entry.Kind == InstalledSoftwareKind.SystemComponent)
                             .ToList())
                {
                    ManualEntries.Remove(item);
                }
            }

            OnPropertyChanged(nameof(ManualHeader));
            OnPropertyChanged(nameof(HasAnySelected));
        }
    }

    /// <summary>Dựng cấu hình sao lưu từ những mục đang được tick.</summary>
    public InstallationProfile BuildProfile(IEnumerable<string> existingProfileNames)
    {
        var packages = AutomaticEntries
            .Where(e => e.IsSelected)
            .Select((e, index) => new SoftwarePackage
            {
                Name = e.Entry.Name,
                PackageId = e.Entry.RawId,
                Version = e.Entry.Version,
                Source = ResolveSource(e.Entry.Source),
                Category = SoftwareCategoryGuesser.Guess(e.Entry.Name, e.Entry.RawId),
                IsSelected = true,
                SortOrder = index
            })
            .ToList();

        return new InstallationProfile
        {
            Name = MakeUniqueName(existingProfileNames),
            Description = $"Sao lưu tự động từ máy {_snapshot.MachineName} lúc {_snapshot.ScannedAt:dd/MM/yyyy HH:mm}.",
            Packages = packages,
            ManualSoftware = ManualEntries.Where(e => e.IsSelected).Select(e => e.Entry).ToList()
        };
    }

    private ScanEntryViewModel Track(ScanEntryViewModel entry)
    {
        entry.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ScanEntryViewModel.IsSelected))
            {
                OnPropertyChanged(nameof(HasAnySelected));
            }
        };

        return entry;
    }

    private string MakeUniqueName(IEnumerable<string> existingProfileNames)
    {
        var baseName = $"Sao lưu {_snapshot.MachineName} {_snapshot.ScannedAt:dd-MM-yyyy}";
        var taken = new HashSet<string>(existingProfileNames, StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName} ({suffix})";

            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string ResolveSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "winget";
        }

        var trimmed = source.Trim();

        return AllowedSources.Contains(trimmed, StringComparer.OrdinalIgnoreCase)
            ? trimmed.ToLowerInvariant()
            : "winget";
    }
}
```

- [ ] **Bước 4: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~ScanResultViewModel"
```

Kết quả mong đợi: **11 test PASS**.

- [ ] **Bước 5: Chạy toàn bộ và commit**

```bash
dotnet test
git add src/WindowsSetupAssistant.App/ViewModels/ScanResultViewModel.cs tests/WindowsSetupAssistant.App.Tests/ScanResultViewModelTests.cs
git commit -m "ViewModel cho cua so xem lai ket qua quet"
```

Kết quả mong đợi trước khi commit: **198 test pass**.

---

## Task 8: Giao diện và nối vào ứng dụng

**Files:**
- Create: `src/WindowsSetupAssistant.App/Views/ScanResultWindow.xaml`
- Create: `src/WindowsSetupAssistant.App/Views/ScanResultWindow.xaml.cs`
- Modify: `src/WindowsSetupAssistant.App/Services/DialogService.cs` (interface + hiện thực)
- Modify: `src/WindowsSetupAssistant.App/ViewModels/MainViewModel.cs` (constructor + lệnh mới)
- Modify: `src/WindowsSetupAssistant.App/App.xaml.cs` (lắp ráp dịch vụ mới)
- Modify: `src/WindowsSetupAssistant.App/Views/MainWindow.xaml` (nút mới)
- Modify: `tests/WindowsSetupAssistant.App.Tests/Fakes/UiFakes.cs` (fake cho 2 cổng mới)
- Modify: `tests/WindowsSetupAssistant.App.Tests/ViewModelFixture.cs` (truyền dịch vụ mới)
- Test: `tests/WindowsSetupAssistant.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `IMachineScanService` (Task 4), `IBackupExporter` + `BackupPaths` (Task 5),
  `ScanResultViewModel.BuildProfile` (Task 7)
- Produces: `MainViewModel.ScanAndBackupCommand` (AsyncRelayCommand);
  `IDialogService.ShowScanResult(ScanResultViewModel) → bool`

- [ ] **Bước 1: Mở rộng `IDialogService` và fake**

Thêm vào `IDialogService` (trong `DialogService.cs`):

```csharp
    bool ShowScanResult(ScanResultViewModel viewModel);
```

Thêm hiện thực vào class `DialogService`:

```csharp
    public bool ShowScanResult(ScanResultViewModel viewModel) =>
        ShowDialog(new ScanResultWindow { DataContext = viewModel });
```

Spec mục 8 yêu cầu hộp thoại lưu file **mặc định mở ở thư mục chứa .exe** để bản sao lưu rơi
thẳng vào USB. Sửa tiếp `SaveJsonFile` trong cùng file, thêm một dòng vào phần khởi tạo:

```csharp
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = JsonFilter,
            FileName = suggestedFileName,
            DefaultExt = ".json",
            AddExtension = true,
            // Mặc định lưu cạnh file .exe - chạy từ USB thì bản sao lưu nằm luôn trên USB.
            InitialDirectory = AppContext.BaseDirectory
        };
```

Thay đổi này ảnh hưởng cả nút "Xuất tất cả" và "Xuất cấu hình này" đang có - đó là hành vi mong
muốn, cùng một lý do. Các test xuất/nhập cũ dùng `UiDialogFake` nên không bị ảnh hưởng.

Thêm vào `UiDialogFake` trong `tests/WindowsSetupAssistant.App.Tests/Fakes/UiFakes.cs`:

```csharp
    public bool ScanResultResult { get; set; } = true;

    /// <summary>Cho test can thiệp vào lựa chọn tick trước khi "bấm Lưu".</summary>
    public Action<ScanResultViewModel>? OnScanResult { get; set; }

    public bool ShowScanResult(ScanResultViewModel viewModel)
    {
        OnScanResult?.Invoke(viewModel);
        return ScanResultResult;
    }
```

Thêm fake cho hai cổng mới vào cùng file:

```csharp
internal sealed class UiScanFake : IMachineScanService
{
    public MachineSnapshot Snapshot { get; set; } = new()
    {
        MachineName = "MAYTHU",
        ScannedAt = DateTimeOffset.Now,
        Entries = Array.Empty<InstalledSoftwareEntry>()
    };

    public Exception? ThrowOnScan { get; set; }

    public int ScanCount { get; private set; }

    public Task<MachineSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScanCount++;

        if (ThrowOnScan is not null)
        {
            throw ThrowOnScan;
        }

        return Task.FromResult(Snapshot);
    }
}

internal sealed class UiBackupExporterFake : IBackupExporter
{
    public List<InstallationProfile> Exported { get; } = new();

    public Exception? ThrowOnExport { get; set; }

    public Task<BackupPaths> ExportAsync(
        InstallationProfile profile,
        string jsonFilePath,
        int schemaVersion = 1,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnExport is not null)
        {
            throw ThrowOnExport;
        }

        Exported.Add(profile);
        return Task.FromResult(new BackupPaths(jsonFilePath, jsonFilePath + "-cai-tay.csv"));
    }
}
```

Bổ sung using cần thiết ở đầu `UiFakes.cs`: `WindowsSetupAssistant.Application.Models`,
`WindowsSetupAssistant.Domain.Entities`, `WindowsSetupAssistant.Domain.Models`.

- [ ] **Bước 2: Viết test thất bại**

Thêm vào `MainViewModelTests`:

```csharp
    [Fact]
    public Task ScanAndBackup_CreatesProfileFromTickedEntriesAndExportsFile() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();

        fixture.Scanner.Snapshot = new MachineSnapshot
        {
            MachineName = "MAYTHU",
            ScannedAt = new DateTimeOffset(2026, 9, 7, 10, 30, 0, TimeSpan.FromHours(7)),
            Entries = new[]
            {
                new InstalledSoftwareEntry("Git", "Git.Git", "2.55", "winget", InstalledSoftwareKind.WingetPackage),
                new InstalledSoftwareEntry("Android Studio", @"ARP\Machine\X64\Android Studio", "2026.1", null,
                    InstalledSoftwareKind.ManualOnly)
            }
        };
        fixture.Dialogs.ExportPath = Path.Combine(fixture.DirectoryPath, "sao-luu.json");

        var profileCountBefore = fixture.ViewModel.Profiles.Count;
        await WpfTestHost.ExecuteAsync(fixture.ViewModel.ScanAndBackupCommand);

        Assert.Equal(1, fixture.Scanner.ScanCount);
        Assert.Equal(profileCountBefore + 1, fixture.ViewModel.Profiles.Count);

        var created = Assert.Single(fixture.Backup.Exported);
        Assert.Equal("Sao lưu MAYTHU 07-09-2026", created.Name);
        Assert.Equal("Git.Git", Assert.Single(created.Packages).PackageId);
        Assert.Equal("Android Studio", Assert.Single(created.ManualSoftware).Name);

        // Cau hinh vua tao phai duoc chon va da luu xuong dia.
        Assert.Equal(created.Name, fixture.ViewModel.SelectedProfile!.Name);
        var reloaded = await fixture.Repository.LoadAsync();
        Assert.Contains(reloaded.Profiles, p => p.Name == created.Name);
        fixture.AssertHealthy();
    });

    [Fact]
    public Task ScanAndBackup_UserCancelsReviewWindow_ChangesNothing() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();

        fixture.Scanner.Snapshot = new MachineSnapshot
        {
            MachineName = "MAYTHU",
            ScannedAt = DateTimeOffset.Now,
            Entries = new[]
            {
                new InstalledSoftwareEntry("Git", "Git.Git", "2.55", "winget", InstalledSoftwareKind.WingetPackage)
            }
        };
        fixture.Dialogs.ScanResultResult = false;

        var profileCountBefore = fixture.ViewModel.Profiles.Count;
        await WpfTestHost.ExecuteAsync(fixture.ViewModel.ScanAndBackupCommand);

        Assert.Equal(profileCountBefore, fixture.ViewModel.Profiles.Count);
        Assert.Empty(fixture.Backup.Exported);
        fixture.AssertHealthy();
    });

    [Fact]
    public Task ScanAndBackup_NothingFound_ShowsMessageAndCreatesNoProfile() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();

        var profileCountBefore = fixture.ViewModel.Profiles.Count;
        await WpfTestHost.ExecuteAsync(fixture.ViewModel.ScanAndBackupCommand);

        Assert.Equal(profileCountBefore, fixture.ViewModel.Profiles.Count);
        Assert.Empty(fixture.Backup.Exported);
        Assert.Contains(fixture.Dialogs.Messages, m => m.Contains("Không tìm thấy phần mềm nào"));
    });

    [Fact]
    public Task ScanAndBackup_ScanFails_ShowsErrorAndKeepsProfilesIntact() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();

        fixture.Scanner.ThrowOnScan = new InvalidOperationException("winget không phản hồi");

        var profileCountBefore = fixture.ViewModel.Profiles.Count;
        await WpfTestHost.ExecuteAsync(fixture.ViewModel.ScanAndBackupCommand);

        Assert.Equal(profileCountBefore, fixture.ViewModel.Profiles.Count);
        Assert.Contains(fixture.Dialogs.Errors, e => e.Contains("winget không phản hồi"));
    });

    [Fact]
    public Task ScanAndBackup_ExportFails_KeepsCreatedProfile() => WpfTestHost.Run(async () =>
    {
        await using var fixture = new ViewModelFixture();
        await fixture.InitializeAsync();

        fixture.Scanner.Snapshot = new MachineSnapshot
        {
            MachineName = "MAYTHU",
            ScannedAt = DateTimeOffset.Now,
            Entries = new[]
            {
                new InstalledSoftwareEntry("Git", "Git.Git", "2.55", "winget", InstalledSoftwareKind.WingetPackage)
            }
        };
        fixture.Dialogs.ExportPath = Path.Combine(fixture.DirectoryPath, "sao-luu.json");
        fixture.Backup.ThrowOnExport = new IOException("USB đã bị rút");

        var profileCountBefore = fixture.ViewModel.Profiles.Count;
        await WpfTestHost.ExecuteAsync(fixture.ViewModel.ScanAndBackupCommand);

        // Ghi file that bai KHONG duoc lam mat cau hinh da tao.
        Assert.Equal(profileCountBefore + 1, fixture.ViewModel.Profiles.Count);
        Assert.Contains(fixture.Dialogs.Errors, e => e.Contains("USB đã bị rút"));
    });
```

Bổ sung using ở đầu `MainViewModelTests.cs` nếu thiếu: `System.IO`,
`WindowsSetupAssistant.Domain.Enums`, `WindowsSetupAssistant.Domain.Models`.

- [ ] **Bước 3: Cập nhật `ViewModelFixture`**

Thêm hai thuộc tính và truyền vào constructor:

```csharp
    public UiScanFake Scanner { get; } = new();
    public UiBackupExporterFake Backup { get; } = new();
```

Sửa lời gọi `new MainViewModel(...)` để truyền thêm `Scanner` và `Backup` (xem thứ tự tham số ở Bước 5).

- [ ] **Bước 4: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~ScanAndBackup"
```

Kết quả mong đợi: **lỗi biên dịch** — `MainViewModel` chưa có `ScanAndBackupCommand` và constructor
chưa nhận hai dịch vụ mới.

- [ ] **Bước 5: Nối vào `MainViewModel`**

Thêm hai trường và hai tham số constructor (đặt **sau** `queueService`, giữ nguyên thứ tự các
tham số còn lại):

```csharp
    private readonly IMachineScanService _scanService;
    private readonly IBackupExporter _backupExporter;
```

```csharp
    public MainViewModel(
        IProfileRepository repository,
        IWingetService wingetService,
        InstallationQueueService queueService,
        IMachineScanService scanService,
        IBackupExporter backupExporter,
        IAppLogger logger,
        IDialogService dialogService,
        ThemeManager themeManager,
        SettingsStore settingsStore,
        AppSettings settings,
        LogViewModel logViewModel)
```

Gán trong thân constructor:

```csharp
        _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
        _backupExporter = backupExporter ?? throw new ArgumentNullException(nameof(backupExporter));
```

Khai báo lệnh (đặt cạnh `RefreshInstalledCommand`):

```csharp
        ScanAndBackupCommand = new AsyncRelayCommand(
            ScanAndBackupAsync,
            () => !_isClosing && !IsInstalling && IsWingetAvailable);
```

Thêm thuộc tính public cạnh các lệnh khác:

```csharp
    public AsyncRelayCommand ScanAndBackupCommand { get; }
```

Thêm `ScanAndBackupCommand.RaiseCanExecuteChanged();` vào `RefreshCommandStates()`.

Thêm phương thức (đặt cạnh `RefreshInstalledStatesAsync`):

```csharp
    /// <summary>
    /// Quét phần mềm đang có trên máy, cho người dùng lọc, rồi lưu thành cấu hình + file sao lưu.
    /// Dùng trước khi cài lại Windows để không phải tìm lại phần mềm sau đó.
    /// </summary>
    private async Task ScanAndBackupAsync()
    {
        IsBusy = true;
        StatusMessage = "Đang quét phần mềm trên máy...";

        MachineSnapshot snapshot;

        try
        {
            snapshot = await _scanService.ScanAsync(_lifetimeCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã huỷ quét.";
            return;
        }
        catch (Exception ex)
        {
            _logger.Error($"Quét máy thất bại: {ex.Message}");
            _dialogService.ShowError("Quét thất bại", ex.Message);
            StatusMessage = "Quét thất bại.";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        if (snapshot.Entries.Count == 0)
        {
            _dialogService.ShowInfo("Quét xong", "Không tìm thấy phần mềm nào trên máy này.");
            StatusMessage = "Không tìm thấy phần mềm nào.";
            return;
        }

        var scanResult = new ScanResultViewModel(snapshot);

        if (!_dialogService.ShowScanResult(scanResult))
        {
            StatusMessage = "Đã huỷ - chưa lưu bản sao lưu nào.";
            return;
        }

        if (!scanResult.HasAnySelected)
        {
            _dialogService.ShowInfo("Chưa chọn gì", "Bạn chưa tick chọn phần mềm nào để sao lưu.");
            return;
        }

        var profile = scanResult.BuildProfile(_catalog.Profiles.Select(p => p.Name));

        _catalog.Profiles.Add(profile);
        Profiles.Add(profile);
        SelectedProfile = profile;
        await SaveCatalogAsync().ConfigureAwait(true);

        StatusMessage = $"Đã tạo cấu hình \"{profile.Name}\".";

        var suggestedName = $"sao-luu-{MakeSafeFileName(snapshot.MachineName)}-{snapshot.ScannedAt:yyyyMMdd-HHmm}.json";
        var path = _dialogService.SaveJsonFile("Lưu bản sao lưu phần mềm", suggestedName);

        if (path is null)
        {
            _dialogService.ShowInfo(
                "Đã tạo cấu hình",
                $"Cấu hình \"{profile.Name}\" đã được lưu trong ứng dụng. Bạn chưa xuất file sao lưu.");
            return;
        }

        try
        {
            var paths = await _backupExporter
                .ExportAsync(profile, path, _catalog.SchemaVersion, _lifetimeCts.Token)
                .ConfigureAwait(true);

            _dialogService.ShowInfo(
                "Sao lưu thành công",
                $"Đã tạo cấu hình \"{profile.Name}\" và ghi 2 file:\n\n{paths.JsonPath}\n{paths.CsvPath}");
        }
        catch (Exception ex)
        {
            // Cau hinh da tao van con nguyen - khong duoc lam mat no.
            _logger.Error($"Ghi file sao lưu thất bại: {ex.Message}");
            _dialogService.ShowError(
                "Ghi file thất bại",
                $"{ex.Message}\n\nCấu hình \"{profile.Name}\" vẫn đã được lưu trong ứng dụng.");
        }
    }
```

Đổi `MakeSafeFileName` từ `private static` sang `private static` giữ nguyên (đã tồn tại, dùng lại được).

Thêm using nếu thiếu: `WindowsSetupAssistant.Domain.Models;`

- [ ] **Bước 6: Lắp ráp trong `App.xaml.cs`**

Sau dòng tạo `queueService`, thêm:

```csharp
        var scanService = new MachineScanService(wingetService, logger);
        var backupExporter = new BackupExporter();
```

và truyền `scanService, backupExporter` vào `new MainViewModel(...)` đúng vị trí thứ 4 và 5.

- [ ] **Bước 7: Chạy test ViewModel**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~ScanAndBackup"
```

Kết quả mong đợi: **5 test PASS**.

- [ ] **Bước 8: Tạo cửa sổ `ScanResultWindow`**

`src/WindowsSetupAssistant.App/Views/ScanResultWindow.xaml`:

```xml
<Window x:Class="WindowsSetupAssistant.App.Views.ScanResultWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:vm="clr-namespace:WindowsSetupAssistant.App.ViewModels"
        mc:Ignorable="d"
        d:DataContext="{d:DesignInstance Type=vm:ScanResultViewModel}"
        Title="Kết quả quét phần mềm"
        Width="900" Height="700"
        WindowStartupLocation="CenterOwner"
        ShowInTaskbar="False"
        Background="{DynamicResource WindowBackgroundBrush}">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Margin="0,0,0,14">
            <TextBlock Text="{Binding Headline}" Style="{StaticResource PageTitleText}" />
            <TextBlock Style="{StaticResource MutedText}" Margin="0,4,0,0"
                       Text="Bỏ tick những phần mềm không cần mang sang máy mới. Nhóm dưới chỉ được ghi lại để cài tay, ứng dụng không tự cài được." />
        </StackPanel>

        <TextBlock Grid.Row="1" Text="{Binding AutomaticHeader}" Style="{StaticResource SectionTitleText}"
                   VerticalAlignment="Top" />
        <DataGrid Grid.Row="1" Margin="0,28,0,10" ItemsSource="{Binding AutomaticEntries}">
            <DataGrid.Columns>
                <DataGridTemplateColumn Header="Giữ" Width="52">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <CheckBox IsChecked="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"
                                      HorizontalAlignment="Center" />
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
                <DataGridTextColumn Header="Tên phần mềm" Binding="{Binding Name}" Width="2*" />
                <DataGridTextColumn Header="WinGet Package Id" Binding="{Binding RawId}" Width="2*"
                                    FontFamily="Consolas" />
                <DataGridTextColumn Header="Phiên bản" Binding="{Binding Version}" Width="130" />
            </DataGrid.Columns>
        </DataGrid>

        <TextBlock Grid.Row="2" Text="{Binding ManualHeader}" Style="{StaticResource SectionTitleText}"
                   VerticalAlignment="Top" />
        <DataGrid Grid.Row="2" Margin="0,28,0,10" ItemsSource="{Binding ManualEntries}">
            <DataGrid.Columns>
                <DataGridTemplateColumn Header="Ghi" Width="52">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <CheckBox IsChecked="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"
                                      HorizontalAlignment="Center" />
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
                <DataGridTextColumn Header="Tên phần mềm" Binding="{Binding Name}" Width="2*" />
                <DataGridTextColumn Header="Phiên bản" Binding="{Binding Version}" Width="130" />
                <DataGridTextColumn Header="Mã định danh" Binding="{Binding RawId}" Width="2*"
                                    FontFamily="Consolas" />
            </DataGrid.Columns>
        </DataGrid>

        <CheckBox Grid.Row="3" Margin="0,4,0,0"
                  Content="{Binding SystemComponentLabel}"
                  IsChecked="{Binding ShowSystemComponents}" />

        <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button Content="Huỷ" IsCancel="True" MinWidth="110" />
            <Button Content="Lưu bản sao lưu" Click="OnSaveClick" IsDefault="True" MinWidth="150"
                    Margin="10,0,0,0" Style="{StaticResource PrimaryButton}" />
        </StackPanel>
    </Grid>
</Window>
```

`src/WindowsSetupAssistant.App/Views/ScanResultWindow.xaml.cs`:

```csharp
using System.Windows;

namespace WindowsSetupAssistant.App.Views;

public partial class ScanResultWindow : Window
{
    public ScanResultWindow() => InitializeComponent();

    private void OnSaveClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
```

- [ ] **Bước 9: Thêm nút vào `MainWindow.xaml`**

Trong thanh hành động dưới cùng của thẻ "Danh sách phần mềm", ngay **trước** nút "Kiểm tra đã cài",
thêm:

```xml
                                <Button Content="Quét &amp; sao lưu máy này"
                                        Command="{Binding ScanAndBackupCommand}"
                                        Margin="0,0,8,0"
                                        ToolTip="Quét phần mềm đang có trên máy và lưu lại trước khi cài lại Windows" />
```

- [ ] **Bước 10: Thêm test binding cho cửa sổ mới**

Thêm vào `MainWindowBindingTests`:

```csharp
    [Fact]
    public Task ScanResultWindowHasCleanBindings() => WpfTestHost.Run(async () =>
    {
        using var trace = new BindingTrace();

        var snapshot = new MachineSnapshot
        {
            MachineName = "MAYTHU",
            ScannedAt = DateTimeOffset.Now,
            Entries = new[]
            {
                new InstalledSoftwareEntry("Git", "Git.Git", "2.55", "winget", InstalledSoftwareKind.WingetPackage),
                new InstalledSoftwareEntry("Android Studio", @"ARP\Machine\X64\Android Studio", "2026.1", null,
                    InstalledSoftwareKind.ManualOnly),
                new InstalledSoftwareEntry("3D Viewer", @"MSIX\Microsoft.Microsoft3DViewer", "7.2", null,
                    InstalledSoftwareKind.SystemComponent)
            }
        };

        var vm = new ScanResultViewModel(snapshot);
        var window = new ScanResultWindow { DataContext = vm };

        try
        {
            await WpfTestHost.ShowAsync(window);

            vm.ShowSystemComponents = true;
            window.UpdateLayout();
            await WpfTestHost.DrainAsync();

            Assert.Equal(2, WpfTestHost.Descendants<DataGrid>(window).Count());
            trace.AssertClean();
        }
        finally
        {
            window.Close();
            await WpfTestHost.WaitUntilAsync(() => !window.IsVisible);
        }
    });
```

- [ ] **Bước 11: Chạy toàn bộ và build**

```bash
dotnet build
dotnet test
```

Kết quả mong đợi: build **0 lỗi 0 cảnh báo**; **204 test pass**.

- [ ] **Bước 12: Commit**

```bash
git add src tests
git commit -m "Them chuc nang quet va sao luu phan mem truoc khi cai lai Windows"
```

---

## Kiểm thử thủ công cuối cùng

Sau khi xong Task 8, chạy kịch bản giao diện sẵn có để chắc chắn không làm hỏng luồng cũ:

```bash
dotnet publish src/WindowsSetupAssistant.App -c Release -r win-x64 -o publish
cd tools/ui-smoke-test
dotnet build fake-winget/winget.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-UiSmokeTest.ps1
```

Kết quả mong đợi: `Dat: 16 | Truot: 0`.

Sau đó chạy app thật một lần, bấm **"Quét & sao lưu máy này"** và kiểm tra:

- [ ] Hai bảng hiện đúng, app hệ thống bị ẩn cho tới khi tick ô "Hiện cả app hệ thống"
- [ ] Bấm Lưu → cấu hình mới xuất hiện trong ô chọn cấu hình và được chọn sẵn
- [ ] Hai file được ghi ra đúng chỗ đã chọn
- [ ] Mở file CSV bằng Excel: tiếng Việt hiển thị đúng, đúng 5 cột
- [ ] Bấm "Nhập JSON" chọn file vừa xuất → khôi phục được cấu hình
