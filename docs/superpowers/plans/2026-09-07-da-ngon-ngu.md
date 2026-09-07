# Kế hoạch triển khai: Hỗ trợ đa ngôn ngữ

> **Dành cho người/agent thực thi:** BẮT BUỘC dùng skill `superpowers:subagent-driven-development`
> (khuyên dùng) hoặc `superpowers:executing-plans`. Các bước dùng checkbox (`- [ ]`) để theo dõi.

**Mục tiêu:** Ứng dụng chạy đầy đủ bằng tiếng Việt, tiếng Trung phồn thể và tiếng Anh, đổi ngôn ngữ
ngay trong lúc chạy mà không cần khởi động lại.

**Kiến trúc:** Tầng dưới trả về **khoá** (`LocalizedText`) thay vì câu chữ; việc dịch dồn về tầng
giao diện qua `IStringLocalizer`. Bản dịch nằm trong `.resx`. Đổi ngôn ngữ tức thì bằng cách bắn
`PropertyChanged("Item[]")` trên một indexer — cùng thủ thuật `ThemeManager` đang dùng cho Sáng/Tối.

**Công nghệ:** .NET 8, WPF, `System.Resources.ResourceManager`, xUnit. Không thêm gói NuGet nào.

**Spec gốc:** `docs/superpowers/specs/2026-09-07-da-ngon-ngu-design.md`

## Ràng buộc toàn cục

- Không thêm dependency NuGet mới.
- Ba ngôn ngữ: `en` (neutral, nhúng trong assembly chính), `vi`, `zh-Hant`.
- **Mốc hiện tại: build 0 lỗi 0 cảnh báo, 161 test pass** (144 + 17). Mọi task phải giữ mốc này.
- Test cũ so khớp chuỗi tiếng Việt sẽ đổi sang so khớp **khoá**. Đây là thay đổi hành vi hợp lệ vì
  kiểu dữ liệu đã đổi — **không phải sửa test để lách**.
- Chú thích code viết tiếng Việt, giữ giọng văn như code hiện có.
- Không test nào gọi winget thật hay ghi file ngoài thư mục tạm.
- Thư mục làm việc cho mọi lệnh: `D:\Desktop\AI\Windows-Setup-Assistant`

### CẢNH BÁO: có phiên khác đang sửa cùng repo

Tại thời điểm viết kế hoạch, cây làm việc đang có 8 thay đổi chưa commit của một phiên khác
(`docs/images/`, `tools/DocScreenshotCapture/`, `docs/build-guide.js`…). Kế hoạch này **chạm vào gần
như mọi file nguồn**, nên phải:

1. Xác nhận `git status` sạch trước khi bắt đầu.
2. Không chạy song song với phiên khác trên cùng repo.

### Lệch spec có chủ đích: không dùng lớp sinh tự động từ .resx

Spec mục 3 ghi "trình biên dịch sinh lớp truy cập nên gõ sai khoá là lỗi biên dịch". Kế hoạch này
**không** bật `GenerateResxSource`, mà dùng hai lớp hằng số: `MessageKeys` (Domain) và `UiKeys` (App).

Lý do: Domain không thể tham chiếu lớp sinh ra trong App, nên nếu dùng lớp sinh tự động sẽ phải
tồn tại song song hai hệ thống khoá. Hằng số cho **an toàn biên dịch tương đương** (gõ sai tên hằng
là lỗi biên dịch) và hoạt động đồng nhất ở mọi tầng. Phần "khoá có tồn tại trong .resx không" được
Task 2 bảo vệ bằng test đối chiếu — mạnh hơn lớp sinh tự động vì nó kiểm tra **cả ba ngôn ngữ**.

---

## Cấu trúc file

| File | Trách nhiệm | Task |
|---|---|---|
| `src/…Domain/Localization/LocalizedText.cs` | Khoá + tham số, không biết dịch thuật | 1 |
| `src/…Domain/Localization/LocalizedException.cs` | Ngoại lệ mang khoá | 1 |
| `src/…Domain/Localization/MessageKeys.cs` | Hằng khoá cho thông điệp tầng dưới | 1 |
| `src/…Application/Abstractions/IStringLocalizer.cs` | Hợp đồng dịch | 2 |
| `src/…App/Localization/Strings.resx` (+ `.vi`, `.zh-Hant`) | Bản dịch | 2 |
| `src/…App/Localization/ResourceStringLocalizer.cs` | Đọc `.resx`, nguồn sự thật | 2 |
| `src/…App/Localization/UiKeys.cs` | Hằng khoá cho chuỗi giao diện | 2 |
| `src/…App/Localization/LocalizationSource.cs` | Lớp bọc cho XAML binding, bắn đổi ngôn ngữ | 3 |
| `src/…App/Localization/TextExtension.cs` | Markup extension `{loc:Text ...}` | 3 |
| `src/…App/Services/AppSettings.cs` | *(sửa)* thêm `Language` | 4 |
| `src/…App/Localization/LanguageCatalog.cs` | 3 ngôn ngữ + dò theo Windows | 4 |
| `src/…App/WindowsSetupAssistant.App.csproj` | *(sửa)* `SatelliteResourceLanguages` | 5 |
| `src/…App/Themes/Controls.xaml` | *(sửa)* font dự phòng chữ Hán | 5 |
| `src/…App/Views/*.xaml` | *(sửa)* chuyển sang `{loc:Text}` | 6, 7 |
| `src/…App/ViewModels/*.cs` | *(sửa)* dùng localizer | 8, 9 |
| `src/…Domain`, `…Application` | *(sửa)* trả `LocalizedText` | 10 |
| `src/…Infrastructure` | *(sửa)* trả `LocalizedText`, log tiếng Anh | 11 |
| `tools/ui-smoke-test/Run-UiSmokeTest.ps1` | *(sửa)* ghim ngôn ngữ `vi` | 12 |

---

## Task 1: Hạ tầng khoá ở Domain

**Files:**
- Create: `src/WindowsSetupAssistant.Domain/Localization/LocalizedText.cs`
- Create: `src/WindowsSetupAssistant.Domain/Localization/LocalizedException.cs`
- Create: `src/WindowsSetupAssistant.Domain/Localization/MessageKeys.cs`
- Test: `tests/WindowsSetupAssistant.Tests/Domain/LocalizedTextTests.cs`

**Interfaces:**
- Consumes: không có
- Produces: `LocalizedText.Of(string key, params object?[] args)`, `LocalizedText.Raw(string text)`,
  thuộc tính `Key`, `Arguments`, `IsRaw`; `LocalizedException(LocalizedText)` với thuộc tính
  `LocalizedMessage`; hằng trong `MessageKeys`. Mọi task sau đều dùng.

- [ ] **Bước 1: Viết test thất bại**

Tạo `tests/WindowsSetupAssistant.Tests/Domain/LocalizedTextTests.cs`:

```csharp
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.Tests.Domain;

public class LocalizedTextTests
{
    [Fact]
    public void Of_KeepsKeyAndArguments()
    {
        var text = LocalizedText.Of(MessageKeys.InstallFailed, "Google.Chrome", 5);

        Assert.Equal(MessageKeys.InstallFailed, text.Key);
        Assert.Equal(new object?[] { "Google.Chrome", 5 }, text.Arguments);
        Assert.False(text.IsRaw);
    }

    [Fact]
    public void Of_WithoutArguments_HasEmptyArgumentList()
    {
        var text = LocalizedText.Of(MessageKeys.InstallSucceeded);

        Assert.Empty(text.Arguments);
    }

    [Fact]
    public void Raw_MarksTextAsNotTranslatable()
    {
        var text = LocalizedText.Raw(@"C:\Users\test\Data\software-list.json");

        Assert.True(text.IsRaw);
        Assert.Equal(@"C:\Users\test\Data\software-list.json", text.Key);
        Assert.Empty(text.Arguments);
    }

    [Fact]
    public void Of_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LocalizedText.Of(null!));
    }

    [Fact]
    public void LocalizedException_CarriesKeyAsExceptionMessage()
    {
        var text = LocalizedText.Of(MessageKeys.PackageIdInvalidCharacters);

        var error = new LocalizedException(text);

        Assert.Same(text, error.LocalizedMessage);
        Assert.Equal(MessageKeys.PackageIdInvalidCharacters, error.Message);
    }

    [Fact]
    public void MessageKeys_AreAllDistinct()
    {
        var values = typeof(MessageKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(values);
        Assert.Equal(values.Count, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void MessageKeys_AllStartWithMsgPrefix()
    {
        var values = typeof(MessageKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        Assert.All(values, v => Assert.StartsWith("Msg_", v, StringComparison.Ordinal));
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~LocalizedText"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'LocalizedText' could not be found`.

- [ ] **Bước 3: Tạo `LocalizedText`**

```csharp
namespace WindowsSetupAssistant.Domain.Localization;

/// <summary>
/// Một thông điệp dành cho người dùng, biểu diễn bằng KHOÁ chứ không phải câu chữ.
///
/// Nhờ vậy tầng Domain, Application và Infrastructure không cần biết ứng dụng đang chạy
/// bằng ngôn ngữ nào - việc dịch là chuyện của tầng giao diện.
/// </summary>
public sealed class LocalizedText
{
    private static readonly object?[] NoArguments = Array.Empty<object?>();

    private LocalizedText(string key, object?[] arguments, bool isRaw)
    {
        Key = key;
        Arguments = arguments;
        IsRaw = isRaw;
    }

    /// <summary>Khoá tra trong .resx. Với văn bản Raw thì đây chính là nội dung.</summary>
    public string Key { get; }

    /// <summary>Tham số điền vào chuỗi định dạng.</summary>
    public IReadOnlyList<object?> Arguments { get; }

    /// <summary>True nghĩa là không dịch, in nguyên văn.</summary>
    public bool IsRaw { get; }

    public static LocalizedText Of(string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new LocalizedText(key, arguments.Length == 0 ? NoArguments : arguments, isRaw: false);
    }

    /// <summary>
    /// Văn bản không dịch được và không nên dịch: tên phần mềm, đường dẫn file,
    /// output nguyên văn của winget.
    /// </summary>
    public static LocalizedText Raw(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LocalizedText(text, NoArguments, isRaw: true);
    }

    public override string ToString() => Key;
}
```

- [ ] **Bước 4: Tạo `LocalizedException`**

```csharp
namespace WindowsSetupAssistant.Domain.Localization;

/// <summary>
/// Ngoại lệ mang theo thông điệp dành cho người dùng dưới dạng khoá.
///
/// Message của Exception giữ nguyên khoá - đủ dùng cho log kỹ thuật và debug,
/// còn nơi hiển thị sẽ đọc LocalizedMessage rồi dịch.
/// </summary>
public class LocalizedException : Exception
{
    public LocalizedException(LocalizedText message)
        : base(message?.Key ?? string.Empty)
    {
        ArgumentNullException.ThrowIfNull(message);
        LocalizedMessage = message;
    }

    public LocalizedException(LocalizedText message, Exception innerException)
        : base(message?.Key ?? string.Empty, innerException)
    {
        ArgumentNullException.ThrowIfNull(message);
        LocalizedMessage = message;
    }

    public LocalizedText LocalizedMessage { get; }
}
```

- [ ] **Bước 5: Tạo `MessageKeys`**

Liệt kê khoá cho **mọi thông điệp của tầng dưới**. Danh sách dưới đây lấy từ các chuỗi tiếng Việt
đang có trong Domain, Application và Infrastructure:

```csharp
namespace WindowsSetupAssistant.Domain.Localization;

/// <summary>
/// Nơi DUY NHẤT liệt kê khoá thông điệp của tầng dưới.
/// Gõ sai tên hằng là lỗi biên dịch; khoá thiếu bản dịch bị test đối chiếu ở Task 2 bắt.
/// </summary>
public static class MessageKeys
{
    // --- Kiểm tra Package Id / từ khoá tìm kiếm ---
    public const string PackageIdEmpty = "Msg_PackageIdEmpty";
    public const string PackageIdTooLong = "Msg_PackageIdTooLong";
    public const string PackageIdStartsWithDash = "Msg_PackageIdStartsWithDash";
    public const string PackageIdInvalidCharacters = "Msg_PackageIdInvalidCharacters";
    public const string SearchQueryEmpty = "Msg_SearchQueryEmpty";
    public const string SearchQueryTooLong = "Msg_SearchQueryTooLong";
    public const string SearchQueryStartsWithDash = "Msg_SearchQueryStartsWithDash";
    public const string SearchQueryControlCharacters = "Msg_SearchQueryControlCharacters";

    // --- Kết quả cài đặt ---
    public const string InstallSucceeded = "Msg_InstallSucceeded";
    public const string UpgradeSucceeded = "Msg_UpgradeSucceeded";
    public const string SkippedAlreadyInstalled = "Msg_SkippedAlreadyInstalled";
    public const string InstallFailed = "Msg_InstallFailed";
    public const string CancelledBeforeStart = "Msg_CancelledBeforeStart";
    public const string CancelledByUser = "Msg_CancelledByUser";
    public const string UnexpectedError = "Msg_UnexpectedError";
    public const string WingetNotResponding = "Msg_WingetNotResponding";
    public const string WingetExecutableMissing = "Msg_WingetExecutableMissing";

    // --- Hàng đợi cài đặt ---
    public const string QueueStarted = "Msg_QueueStarted";
    public const string QueueFinished = "Msg_QueueFinished";
    public const string QueueStoppedOnFirstError = "Msg_QueueStoppedOnFirstError";
    public const string QueueProcessing = "Msg_QueueProcessing";
    public const string QueueItemDone = "Msg_QueueItemDone";
    public const string QueueCompleted = "Msg_QueueCompleted";
    public const string QueueCancelled = "Msg_QueueCancelled";
    public const string InstalledStateCheckFailed = "Msg_InstalledStateCheckFailed";

    // --- Mã lỗi WinGet (một khoá cho mỗi nhánh của WingetExitCodes.Describe) ---
    public const string ExitSuccess = "Msg_ExitSuccess";
    public const string ExitNoApplicationsFound = "Msg_ExitNoApplicationsFound";
    public const string ExitAlreadyInstalled = "Msg_ExitAlreadyInstalled";
    public const string ExitNoUpgrade = "Msg_ExitNoUpgrade";
    public const string ExitRequiresAdmin = "Msg_ExitRequiresAdmin";
    public const string ExitAccessDenied = "Msg_ExitAccessDenied";
    public const string ExitProhibitsElevation = "Msg_ExitProhibitsElevation";
    public const string ExitDownloadFailed = "Msg_ExitDownloadFailed";
    public const string ExitZeroByteFile = "Msg_ExitZeroByteFile";
    public const string ExitNoNetwork = "Msg_ExitNoNetwork";
    public const string ExitHashMismatch = "Msg_ExitHashMismatch";
    public const string ExitNoApplicableInstaller = "Msg_ExitNoApplicableInstaller";
    public const string ExitNoSources = "Msg_ExitNoSources";
    public const string ExitSourceOpenFailed = "Msg_ExitSourceOpenFailed";
    public const string ExitServiceUnavailable = "Msg_ExitServiceUnavailable";
    public const string ExitMultipleFound = "Msg_ExitMultipleFound";
    public const string ExitInstallInProgress = "Msg_ExitInstallInProgress";
    public const string ExitPackageInUse = "Msg_ExitPackageInUse";
    public const string ExitMissingDependency = "Msg_ExitMissingDependency";
    public const string ExitDiskFull = "Msg_ExitDiskFull";
    public const string ExitInsufficientMemory = "Msg_ExitInsufficientMemory";
    public const string ExitRebootRequired = "Msg_ExitRebootRequired";
    public const string ExitCancelled = "Msg_ExitCancelled";
    public const string ExitDowngrade = "Msg_ExitDowngrade";
    public const string ExitBlockedByPolicy = "Msg_ExitBlockedByPolicy";
    public const string ExitInvalidArguments = "Msg_ExitInvalidArguments";
    public const string ExitCustomInstallerError = "Msg_ExitCustomInstallerError";
    public const string ExitInternalError = "Msg_ExitInternalError";
    public const string ExitCommandFailed = "Msg_ExitCommandFailed";
    public const string ExitUnknown = "Msg_ExitUnknown";

    // --- Dữ liệu / lưu trữ ---
    public const string SeedCatalogCreated = "Msg_SeedCatalogCreated";
    public const string CatalogFileCorrupted = "Msg_CatalogFileCorrupted";
    public const string CatalogReadFailed = "Msg_CatalogReadFailed";
    public const string CatalogExported = "Msg_CatalogExported";
    public const string CatalogImported = "Msg_CatalogImported";
    public const string ImportFileNotFound = "Msg_ImportFileNotFound";
    public const string ImportInvalidJson = "Msg_ImportInvalidJson";
    public const string ImportNoProfiles = "Msg_ImportNoProfiles";
    public const string PackageDroppedInvalidId = "Msg_PackageDroppedInvalidId";
    public const string SaveFailed = "Msg_SaveFailed";

    // --- WinGet / môi trường ---
    public const string WingetDetected = "Msg_WingetDetected";
    public const string WingetNotFound = "Msg_WingetNotFound";
    public const string WingetCheckFailed = "Msg_WingetCheckFailed";
    public const string WingetReturnedError = "Msg_WingetReturnedError";
    public const string WingetSearchFailed = "Msg_WingetSearchFailed";
    public const string WingetSearchTimeout = "Msg_WingetSearchTimeout";
    public const string InstalledListFailed = "Msg_InstalledListFailed";
    public const string InstalledListTimeout = "Msg_InstalledListTimeout";

    // --- Quét và sao lưu ---
    public const string ScanStarted = "Msg_ScanStarted";
    public const string ScanFinished = "Msg_ScanFinished";
    public const string ScanFailed = "Msg_ScanFailed";

    // --- Nhật ký chung ---
    public const string CommandSucceeded = "Msg_CommandSucceeded";
    public const string CommandFailed = "Msg_CommandFailed";
    public const string AppStarted = "Msg_AppStarted";
    public const string UnhandledError = "Msg_UnhandledError";
}
```

- [ ] **Bước 6: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.Tests --filter "FullyQualifiedName~LocalizedText"
```

Kết quả mong đợi: **7 test PASS**.

- [ ] **Bước 7: Chạy toàn bộ và commit**

```bash
dotnet test
git add src/WindowsSetupAssistant.Domain/Localization tests/WindowsSetupAssistant.Tests/Domain/LocalizedTextTests.cs
git commit -m "Them ha tang khoa thong diep o tang Domain"
```

Kết quả mong đợi: **168 test pass** (161 + 7).

---

## Task 2: `.resx`, localizer và test đối chiếu khoá

Đây là task quan trọng nhất của cả kế hoạch: **test đối chiếu khoá** là thứ duy nhất giữ cho 1170
bản dịch không rơi rớt âm thầm.

**Files:**
- Create: `src/WindowsSetupAssistant.Application/Abstractions/IStringLocalizer.cs`
- Create: `src/WindowsSetupAssistant.App/Localization/Strings.resx` (neutral = tiếng Anh)
- Create: `src/WindowsSetupAssistant.App/Localization/Strings.vi.resx`
- Create: `src/WindowsSetupAssistant.App/Localization/Strings.zh-Hant.resx`
- Create: `src/WindowsSetupAssistant.App/Localization/UiKeys.cs`
- Create: `src/WindowsSetupAssistant.App/Localization/ResourceStringLocalizer.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Localization/ResourceParityTests.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Localization/ResourceStringLocalizerTests.cs`

**Interfaces:**
- Consumes: `LocalizedText`, `MessageKeys` (Task 1)
- Produces:
  - `IStringLocalizer` với `string this[string key]`, `string Format(LocalizedText)`,
    `string Format(LocalizedText, CultureInfo)`
  - `ResourceStringLocalizer` — hiện thực, constructor không tham số
  - `UiKeys` — hằng khoá giao diện, mọi khoá bắt đầu bằng `Ui_`

- [ ] **Bước 1: Viết test đối chiếu khoá (test quan trọng nhất)**

Tạo `tests/WindowsSetupAssistant.App.Tests/Localization/ResourceParityTests.cs`:

```csharp
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

/// <summary>
/// Đối chiếu ba file .resx với nhau và với danh sách hằng khoá.
/// Đây là lưới an toàn cho ~1170 bản dịch: thiếu một chỗ là test đỏ ngay.
/// </summary>
public class ResourceParityTests
{
    private static string LocalizationDirectory([CallerFilePath] string thisFile = "")
    {
        // <repo>/tests/WindowsSetupAssistant.App.Tests/Localization/ResourceParityTests.cs
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
        return Path.Combine(repositoryRoot, "src", "WindowsSetupAssistant.App", "Localization");
    }

    private static Dictionary<string, string> ReadResx(string fileName)
    {
        var path = Path.Combine(LocalizationDirectory(), fileName);
        Assert.True(File.Exists(path), $"Không tìm thấy {path}");

        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .ToDictionary(
                e => e.Attribute("name")!.Value,
                e => e.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static IEnumerable<string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void VietnameseHasEveryKeyOfEnglish()
    {
        var english = ReadResx("Strings.resx");
        var vietnamese = ReadResx("Strings.vi.resx");

        var missing = english.Keys.Except(vietnamese.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();

        Assert.True(missing.Count == 0, "Thiếu bản dịch tiếng Việt cho: " + string.Join(", ", missing));
    }

    [Fact]
    public void TraditionalChineseHasEveryKeyOfEnglish()
    {
        var english = ReadResx("Strings.resx");
        var chinese = ReadResx("Strings.zh-Hant.resx");

        var missing = english.Keys.Except(chinese.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();

        Assert.True(missing.Count == 0, "Thiếu bản dịch tiếng Trung cho: " + string.Join(", ", missing));
    }

    [Fact]
    public void NoLanguageHasExtraKeys()
    {
        var english = ReadResx("Strings.resx");

        foreach (var fileName in new[] { "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var extra = ReadResx(fileName).Keys.Except(english.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();
            Assert.True(extra.Count == 0, $"{fileName} có khoá thừa: " + string.Join(", ", extra));
        }
    }

    [Fact]
    public void NoValueIsEmpty()
    {
        foreach (var fileName in new[] { "Strings.resx", "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var empty = ReadResx(fileName)
                .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(k => k)
                .ToList();

            Assert.True(empty.Count == 0, $"{fileName} có khoá bỏ trống: " + string.Join(", ", empty));
        }
    }

    [Fact]
    public void EveryMessageKeyConstantExistsInAllLanguages()
    {
        var keys = ConstantsOf(typeof(MessageKeys)).ToList();

        foreach (var fileName in new[] { "Strings.resx", "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var resource = ReadResx(fileName);
            var missing = keys.Except(resource.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"{fileName} thiếu khoá MessageKeys: " + string.Join(", ", missing));
        }
    }

    [Fact]
    public void EveryUiKeyConstantExistsInAllLanguages()
    {
        var keys = ConstantsOf(typeof(UiKeys)).ToList();
        Assert.NotEmpty(keys);

        foreach (var fileName in new[] { "Strings.resx", "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var resource = ReadResx(fileName);
            var missing = keys.Except(resource.Keys, StringComparer.Ordinal).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"{fileName} thiếu khoá UiKeys: " + string.Join(", ", missing));
        }
    }

    [Fact]
    public void PlaceholderCountsMatchAcrossLanguages()
    {
        // Chuoi tieng Viet co {0} ma tieng Trung quen -> string.Format van chay nhung mat thong tin.
        var english = ReadResx("Strings.resx");

        foreach (var fileName in new[] { "Strings.vi.resx", "Strings.zh-Hant.resx" })
        {
            var other = ReadResx(fileName);

            foreach (var pair in english)
            {
                if (!other.TryGetValue(pair.Key, out var translated))
                {
                    continue;
                }

                Assert.True(
                    CountPlaceholders(pair.Value) == CountPlaceholders(translated),
                    $"{fileName}: khoá {pair.Key} có số tham số không khớp bản tiếng Anh");
            }
        }
    }

    private static int CountPlaceholders(string value)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (System.Text.RegularExpressions.Match match in
                 System.Text.RegularExpressions.Regex.Matches(value, @"\{(\d+)\}"))
        {
            found.Add(match.Groups[1].Value);
        }

        return found.Count;
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~ResourceParity"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'UiKeys' could not be found`.

- [ ] **Bước 3: Tạo `IStringLocalizer`**

`src/WindowsSetupAssistant.Application/Abstractions/IStringLocalizer.cs`:

```csharp
using System.Globalization;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.Application.Abstractions;

/// <summary>
/// Hợp đồng dịch. Khai báo ở tầng Application chứ không phải App vì Infrastructure
/// cũng cần nó: AppLogger phải dịch sang tiếng Anh khi ghi file log.
/// </summary>
public interface IStringLocalizer
{
    /// <summary>Lấy chuỗi theo khoá. Khoá không tồn tại thì trả về chính khoá đó.</summary>
    string this[string key] { get; }

    /// <summary>Dịch và điền tham số theo ngôn ngữ đang dùng.</summary>
    string Format(LocalizedText text);

    /// <summary>Dịch theo một ngôn ngữ chỉ định.</summary>
    string Format(LocalizedText text, CultureInfo culture);
}
```

- [ ] **Bước 4: Tạo `UiKeys`**

`src/WindowsSetupAssistant.App/Localization/UiKeys.cs` — khoá cho chuỗi giao diện. Danh sách đầy đủ
được bổ sung dần ở Task 6-9; ở task này khai báo đủ phần khung để test chạy được:

```csharp
namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Nơi DUY NHẤT liệt kê khoá chuỗi giao diện.
/// Mỗi khi chuyển một chuỗi cứng sang khoá, thêm hằng vào đây và thêm giá trị vào cả 3 file .resx.
/// Test đối chiếu ở ResourceParityTests sẽ bắt ngay nếu quên một ngôn ngữ.
/// </summary>
public static class UiKeys
{
    // --- Thanh tiêu đề ---
    public const string AppTitle = "Ui_AppTitle";
    public const string AppSubtitle = "Ui_AppSubtitle";
    public const string ProfileLabel = "Ui_ProfileLabel";
    public const string ProfileNew = "Ui_ProfileNew";
    public const string ProfileRename = "Ui_ProfileRename";
    public const string ProfileDuplicate = "Ui_ProfileDuplicate";
    public const string ProfileDelete = "Ui_ProfileDelete";
    public const string ThemeDark = "Ui_ThemeDark";
    public const string ThemeLight = "Ui_ThemeLight";
    public const string LanguageLabel = "Ui_LanguageLabel";
}
```

- [ ] **Bước 5: Tạo ba file `.resx`**

Cả ba file dùng đúng phần header chuẩn của `.resx`. Tạo `Strings.resx` (tiếng Anh, neutral):

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089</value></resheader>

  <data name="Ui_AppTitle" xml:space="preserve"><value>Windows Setup Assistant</value></data>
  <data name="Ui_AppSubtitle" xml:space="preserve"><value>Install software in bulk with WinGet after reinstalling Windows</value></data>
  <data name="Ui_ProfileLabel" xml:space="preserve"><value>Profile:</value></data>
  <data name="Ui_ProfileNew" xml:space="preserve"><value>New</value></data>
  <data name="Ui_ProfileRename" xml:space="preserve"><value>Rename</value></data>
  <data name="Ui_ProfileDuplicate" xml:space="preserve"><value>Duplicate</value></data>
  <data name="Ui_ProfileDelete" xml:space="preserve"><value>Delete</value></data>
  <data name="Ui_ThemeDark" xml:space="preserve"><value>Dark theme</value></data>
  <data name="Ui_ThemeLight" xml:space="preserve"><value>Light theme</value></data>
  <data name="Ui_LanguageLabel" xml:space="preserve"><value>Language:</value></data>

  <data name="Msg_PackageIdEmpty" xml:space="preserve"><value>Package Id must not be empty.</value></data>
  <data name="Msg_PackageIdTooLong" xml:space="preserve"><value>Package Id is too long (maximum {0} characters).</value></data>
  <data name="Msg_PackageIdStartsWithDash" xml:space="preserve"><value>Package Id must not start with '-' because it would be read as a command-line switch.</value></data>
  <data name="Msg_PackageIdInvalidCharacters" xml:space="preserve"><value>Package Id may only contain letters, digits and . _ - + (for example Google.Chrome).</value></data>
  <data name="Msg_InstallSucceeded" xml:space="preserve"><value>Installed successfully.</value></data>
  <data name="Msg_UpgradeSucceeded" xml:space="preserve"><value>Upgraded successfully.</value></data>
  <data name="Msg_ExitUnknown" xml:space="preserve"><value>WinGet exited with error code 0x{0:X8}.</value></data>
</root>
```

Tạo `Strings.vi.resx` với **cùng bộ khoá**, giá trị là các câu tiếng Việt đang có trong code
(ví dụ `Msg_PackageIdEmpty` = `Package Id không được để trống.`), và `Strings.zh-Hant.resx` với
bản dịch tiếng Trung phồn thể.

> Ở task này chỉ cần đủ khoá đã khai báo trong `UiKeys` và các khoá `MessageKeys` xuất hiện trong
> `Strings.resx` ở trên. Các task sau sẽ bổ sung dần; test đối chiếu bảo đảm ba file luôn khớp nhau.

Đăng ký file vào csproj — thêm vào `WindowsSetupAssistant.App.csproj`:

```xml
  <ItemGroup>
    <EmbeddedResource Update="Localization\Strings.resx">
      <LogicalName>WindowsSetupAssistant.App.Localization.Strings.resources</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
```

- [ ] **Bước 6: Tạo `ResourceStringLocalizer`**

```csharp
using System.Globalization;
using System.Resources;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Đọc bản dịch từ .resx. Đây là NGUỒN SỰ THẬT của việc dịch;
/// LocalizationSource chỉ là lớp bọc phục vụ binding trong XAML.
/// </summary>
public sealed class ResourceStringLocalizer : IStringLocalizer
{
    private readonly ResourceManager _resourceManager = new(
        "WindowsSetupAssistant.App.Localization.Strings",
        typeof(ResourceStringLocalizer).Assembly);

    public string this[string key] => Get(key, CultureInfo.CurrentUICulture);

    public string Format(LocalizedText text) => Format(text, CultureInfo.CurrentUICulture);

    public string Format(LocalizedText text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);

        // Văn bản Raw là đường dẫn, tên phần mềm, output winget - in nguyên văn.
        if (text.IsRaw)
        {
            return text.Key;
        }

        var pattern = Get(text.Key, culture);

        if (text.Arguments.Count == 0)
        {
            return pattern;
        }

        try
        {
            return string.Format(culture, pattern, text.Arguments.ToArray());
        }
        catch (FormatException)
        {
            // Bản dịch sai số tham số - trả chuỗi chưa điền còn hơn làm sập ứng dụng.
            return pattern;
        }
    }

    private string Get(string key, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        try
        {
            // Khoá không có bản dịch thì trả về chính khoá - rất dễ nhận ra khi kiểm thử.
            return _resourceManager.GetString(key, culture) ?? key;
        }
        catch (MissingManifestResourceException)
        {
            return key;
        }
    }
}
```

- [ ] **Bước 7: Viết test cho localizer**

Tạo `tests/WindowsSetupAssistant.App.Tests/Localization/ResourceStringLocalizerTests.cs`:

```csharp
using System.Globalization;
using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class ResourceStringLocalizerTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");
    private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-Hant");

    private readonly ResourceStringLocalizer _localizer = new();

    [Fact]
    public void MissingKey_ReturnsTheKeyItself()
    {
        Assert.Equal("Khong_Ton_Tai", _localizer["Khong_Ton_Tai"]);
    }

    [Fact]
    public void RawText_IsReturnedUnchanged()
    {
        var path = @"C:\Users\test\Data\software-list.json";

        Assert.Equal(path, _localizer.Format(LocalizedText.Raw(path)));
    }

    [Fact]
    public void Format_FillsArguments()
    {
        var text = LocalizedText.Of(MessageKeys.PackageIdTooLong, 200);

        var result = _localizer.Format(text, English);

        Assert.Contains("200", result);
        Assert.DoesNotContain("{0}", result);
    }

    [Fact]
    public void Format_WrongArgumentCount_DoesNotThrow()
    {
        // Bản dịch chờ 1 tham số nhưng không truyền tham số nào.
        var text = LocalizedText.Of(MessageKeys.PackageIdTooLong);

        var result = _localizer.Format(text, English);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    [InlineData("zh-Hant")]
    public void EveryLanguageResolvesAKnownKey(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        var result = _localizer.Format(LocalizedText.Of(MessageKeys.InstallSucceeded), culture);

        Assert.NotEqual(MessageKeys.InstallSucceeded, result);
    }

    [Fact]
    public void ThreeLanguagesGiveThreeDifferentStrings()
    {
        var text = LocalizedText.Of(MessageKeys.InstallSucceeded);

        var english = _localizer.Format(text, English);
        var vietnamese = _localizer.Format(text, Vietnamese);
        var chinese = _localizer.Format(text, Chinese);

        Assert.NotEqual(english, vietnamese);
        Assert.NotEqual(english, chinese);
        Assert.NotEqual(vietnamese, chinese);
    }
}
```

- [ ] **Bước 8: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~Localization"
```

Kết quả mong đợi: **15 test PASS** (7 đối chiếu + 8 localizer).

- [ ] **Bước 9: Chạy toàn bộ và commit**

```bash
dotnet build
dotnet test
git add src/WindowsSetupAssistant.Application/Abstractions/IStringLocalizer.cs src/WindowsSetupAssistant.App/Localization src/WindowsSetupAssistant.App/WindowsSetupAssistant.App.csproj tests/WindowsSetupAssistant.App.Tests/Localization
git commit -m "Them .resx ba ngon ngu, localizer va test doi chieu khoa"
```

Kết quả mong đợi: build 0 lỗi 0 cảnh báo; **183 test pass** (168 + 15).

---

## Task 3: Đổi ngôn ngữ tức thì trong XAML

**Files:**
- Create: `src/WindowsSetupAssistant.App/Localization/LocalizationSource.cs`
- Create: `src/WindowsSetupAssistant.App/Localization/TextExtension.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Localization/LocalizationSourceTests.cs`

**Interfaces:**
- Consumes: `ResourceStringLocalizer` (Task 2)
- Produces: `LocalizationSource.Instance`, `LocalizationSource.SetLanguage(CultureInfo)`,
  indexer `this[string key]`, sự kiện `LanguageChanged`; markup extension dùng trong XAML dạng
  `{loc:Text Ui_ProfileNew}`

- [ ] **Bước 1: Viết test thất bại**

```csharp
using System.ComponentModel;
using System.Globalization;
using WindowsSetupAssistant.App.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class LocalizationSourceTests
{
    [Fact]
    public void SetLanguage_RaisesIndexerPropertyChanged()
    {
        var source = LocalizationSource.Instance;
        var raised = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => raised.Add(e.PropertyName);
        source.PropertyChanged += handler;

        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("en"));
        }
        finally
        {
            source.PropertyChanged -= handler;
        }

        // "Item[]" la ten dac biet: WPF hieu la moi binding toi indexer deu phai cap nhat.
        Assert.Contains("Item[]", raised);
    }

    [Fact]
    public void SetLanguage_SetsAllFourCultureProperties()
    {
        var chinese = CultureInfo.GetCultureInfo("zh-Hant");

        LocalizationSource.Instance.SetLanguage(chinese);

        try
        {
            Assert.Equal(chinese, CultureInfo.CurrentUICulture);
            Assert.Equal(chinese, CultureInfo.CurrentCulture);
            Assert.Equal(chinese, CultureInfo.DefaultThreadCurrentUICulture);
            Assert.Equal(chinese, CultureInfo.DefaultThreadCurrentCulture);
        }
        finally
        {
            LocalizationSource.Instance.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }
    }

    [Fact]
    public void SetLanguage_RaisesLanguageChangedEvent()
    {
        var source = LocalizationSource.Instance;
        CultureInfo? received = null;
        EventHandler<CultureInfo> handler = (_, culture) => received = culture;
        source.LanguageChanged += handler;

        try
        {
            source.SetLanguage(CultureInfo.GetCultureInfo("en"));
        }
        finally
        {
            source.LanguageChanged -= handler;
            source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        }

        Assert.Equal("en", received?.Name);
    }

    [Fact]
    public void Indexer_ReturnsTranslationOfCurrentLanguage()
    {
        var source = LocalizationSource.Instance;

        source.SetLanguage(CultureInfo.GetCultureInfo("en"));
        var english = source["Ui_ProfileNew"];

        source.SetLanguage(CultureInfo.GetCultureInfo("vi"));
        var vietnamese = source["Ui_ProfileNew"];

        Assert.NotEqual(english, vietnamese);
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~LocalizationSource"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'LocalizationSource' could not be found`.

- [ ] **Bước 3: Tạo `LocalizationSource`**

```csharp
using System.ComponentModel;
using System.Globalization;
using WindowsSetupAssistant.Application.Abstractions;

namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Lớp bọc singleton phục vụ binding trong XAML.
///
/// Cách hoạt động giống hệt ThemeManager tráo Light/Dark: khi đổi ngôn ngữ, bắn
/// PropertyChanged với tên đặc biệt "Item[]" - WPF hiểu là MỌI binding tới indexer
/// phải lấy giá trị mới, nên toàn bộ chuỗi trên màn hình đổi cùng lúc.
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationSource> LazyInstance = new(() => new LocalizationSource());

    private LocalizationSource()
    {
        Localizer = new ResourceStringLocalizer();
    }

    public static LocalizationSource Instance => LazyInstance.Value;

    /// <summary>Nguồn sự thật của việc dịch; ViewModel và AppLogger dùng chung đối tượng này.</summary>
    public IStringLocalizer Localizer { get; }

    public CultureInfo CurrentLanguage { get; private set; } = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Bắn sau khi đã đổi ngôn ngữ, để ViewModel tự làm mới chuỗi của mình.</summary>
    public event EventHandler<CultureInfo>? LanguageChanged;

    public string this[string key] => Localizer[key];

    public void SetLanguage(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        CurrentLanguage = culture;

        // Đặt cả bốn để chuỗi, số và ngày tháng đều theo ngôn ngữ đã chọn.
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, culture);
    }
}
```

- [ ] **Bước 4: Tạo markup extension**

```csharp
using System.Windows.Data;
using System.Windows.Markup;

namespace WindowsSetupAssistant.App.Localization;

/// <summary>
/// Dùng trong XAML: Content="{loc:Text Ui_ProfileNew}"
///
/// Trả về một Binding tới indexer của LocalizationSource, nhờ vậy khi đổi ngôn ngữ
/// thì chuỗi tự cập nhật mà không cần vẽ lại cửa sổ.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TextExtension : MarkupExtension
{
    public TextExtension()
    {
    }

    public TextExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationSource.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
```

Khai báo namespace XAML — thêm vào `App.xaml` **và** mọi cửa sổ dùng nó:

```xml
xmlns:loc="clr-namespace:WindowsSetupAssistant.App.Localization"
```

- [ ] **Bước 5: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~LocalizationSource"
```

Kết quả mong đợi: **4 test PASS**.

- [ ] **Bước 6: Chạy toàn bộ và commit**

```bash
dotnet test
git add src/WindowsSetupAssistant.App/Localization tests/WindowsSetupAssistant.App.Tests/Localization/LocalizationSourceTests.cs
git commit -m "Doi ngon ngu tuc thi cho XAML qua markup extension"
```

Kết quả mong đợi: **187 test pass**.

---

## Task 4: Thiết lập ngôn ngữ và dò theo Windows

**Files:**
- Create: `src/WindowsSetupAssistant.App/Localization/LanguageCatalog.cs`
- Modify: `src/WindowsSetupAssistant.App/Services/AppSettings.cs`
- Test: `tests/WindowsSetupAssistant.App.Tests/Localization/LanguageCatalogTests.cs`

**Interfaces:**
- Consumes: không có
- Produces: `LanguageCatalog.Supported` (danh sách `LanguageOption`),
  `LanguageCatalog.Detect(CultureInfo installed) → string`,
  `LanguageCatalog.Resolve(string? saved, CultureInfo installed) → CultureInfo`;
  `record LanguageOption(string Code, string DisplayName)`; `AppSettings.Language`

- [ ] **Bước 1: Viết test thất bại**

```csharp
using System.Globalization;
using WindowsSetupAssistant.App.Localization;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class LanguageCatalogTests
{
    [Fact]
    public void Supported_HasExactlyThreeLanguages()
    {
        Assert.Equal(new[] { "en", "vi", "zh-Hant" }, LanguageCatalog.Supported.Select(l => l.Code).OrderBy(c => c));
    }

    [Fact]
    public void Supported_DisplayNamesAreInTheirOwnLanguage()
    {
        // Ten ngon ngu luon hien bang chinh ngon ngu do, khong dich - de nguoi dung nhan ra.
        Assert.Contains(LanguageCatalog.Supported, l => l.Code == "vi" && l.DisplayName == "Tiếng Việt");
        Assert.Contains(LanguageCatalog.Supported, l => l.Code == "en" && l.DisplayName == "English");
        Assert.Contains(LanguageCatalog.Supported, l => l.Code == "zh-Hant" && l.DisplayName == "繁體中文");
    }

    [Theory]
    [InlineData("vi-VN", "vi")]
    [InlineData("vi", "vi")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-Hant-TW", "zh-Hant")]
    [InlineData("zh-CN", "zh-Hant")]
    [InlineData("en-US", "en")]
    [InlineData("fr-FR", "en")]
    [InlineData("ja-JP", "en")]
    public void Detect_MapsWindowsLanguageToSupportedOne(string installed, string expected)
    {
        Assert.Equal(expected, LanguageCatalog.Detect(CultureInfo.GetCultureInfo(installed)));
    }

    [Fact]
    public void Resolve_SavedValueWins()
    {
        var culture = LanguageCatalog.Resolve("zh-Hant", CultureInfo.GetCultureInfo("vi-VN"));

        Assert.Equal("zh-Hant", culture.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("khong-hop-le")]
    [InlineData("de-DE")]
    public void Resolve_InvalidOrMissingSavedValue_FallsBackToDetection(string? saved)
    {
        var culture = LanguageCatalog.Resolve(saved, CultureInfo.GetCultureInfo("vi-VN"));

        Assert.Equal("vi", culture.Name);
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~LanguageCatalog"
```

Kết quả mong đợi: **lỗi biên dịch** `CS0246: 'LanguageCatalog' could not be found`.

- [ ] **Bước 3: Tạo `LanguageCatalog`**

```csharp
using System.Globalization;

namespace WindowsSetupAssistant.App.Localization;

/// <param name="Code">Mã culture dùng cho .resx.</param>
/// <param name="DisplayName">Tên hiển thị, luôn viết bằng chính ngôn ngữ đó.</param>
public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Ba ngôn ngữ được hỗ trợ và cách chọn ngôn ngữ ở lần chạy đầu.</summary>
public static class LanguageCatalog
{
    public const string English = "en";
    public const string Vietnamese = "vi";
    public const string TraditionalChinese = "zh-Hant";

    public static IReadOnlyList<LanguageOption> Supported { get; } = new[]
    {
        new LanguageOption(Vietnamese, "Tiếng Việt"),
        new LanguageOption(English, "English"),
        new LanguageOption(TraditionalChinese, "繁體中文")
    };

    /// <summary>
    /// Dò ngôn ngữ theo Windows. Mọi biến thể tiếng Trung đều về phồn thể vì đây là
    /// bản duy nhất ứng dụng có - thà hiện chữ Hán còn hơn rơi về tiếng Anh.
    /// </summary>
    public static string Detect(CultureInfo installed)
    {
        ArgumentNullException.ThrowIfNull(installed);

        var name = installed.Name;

        if (name.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
        {
            return Vietnamese;
        }

        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return TraditionalChinese;
        }

        return English;
    }

    /// <summary>Giá trị đã lưu được ưu tiên; không hợp lệ thì dò lại theo Windows.</summary>
    public static CultureInfo Resolve(string? savedCode, CultureInfo installed)
    {
        var code = Supported.Any(l => string.Equals(l.Code, savedCode, StringComparison.OrdinalIgnoreCase))
            ? savedCode!
            : Detect(installed);

        return CultureInfo.GetCultureInfo(code);
    }
}
```

- [ ] **Bước 4: Thêm `Language` vào `AppSettings`**

Thêm vào class `AppSettings` (cạnh `Theme`):

```csharp
    /// <summary>Mã ngôn ngữ đã chọn. Rỗng nghĩa là chưa chọn - sẽ dò theo Windows.</summary>
    public string Language { get; set; } = string.Empty;
```

- [ ] **Bước 5: Chạy test và commit**

```bash
dotnet test
git add src/WindowsSetupAssistant.App/Localization/LanguageCatalog.cs src/WindowsSetupAssistant.App/Services/AppSettings.cs tests/WindowsSetupAssistant.App.Tests/Localization/LanguageCatalogTests.cs
git commit -m "Them danh muc ngon ngu va do ngon ngu theo Windows"
```

Kết quả mong đợi: **203 test pass** (187 + 16).

---

## Task 5: Cấu hình publish, font chữ Hán và bộ chọn ngôn ngữ

Sau task này, ứng dụng đã đổi được ngôn ngữ thật — dù mới chỉ có vài chuỗi được chuyển.

**Files:**
- Modify: `src/WindowsSetupAssistant.App/WindowsSetupAssistant.App.csproj`
- Modify: `src/WindowsSetupAssistant.App/Themes/Controls.xaml` (khoá `AppFontFamily`)
- Modify: `src/WindowsSetupAssistant.App/App.xaml.cs` (khởi tạo ngôn ngữ lúc chạy)
- Modify: `src/WindowsSetupAssistant.App/ViewModels/MainViewModel.cs` (thuộc tính + lệnh đổi ngôn ngữ)
- Modify: `src/WindowsSetupAssistant.App/Views/MainWindow.xaml` (ComboBox chọn ngôn ngữ)
- Test: `tests/WindowsSetupAssistant.App.Tests/Localization/PublishConfigurationTests.cs`

**Interfaces:**
- Consumes: `LanguageCatalog`, `LocalizationSource` (Task 3, 4)
- Produces: `MainViewModel.Languages`, `MainViewModel.SelectedLanguage` (kiểu `LanguageOption`)

- [ ] **Bước 1: Viết test cho cấu hình publish**

Cái bẫy `SatelliteResourceLanguages` im lặng nên phải có test canh:

```csharp
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace WindowsSetupAssistant.App.Tests.Localization;

public class PublishConfigurationTests
{
    private static string ProjectFile([CallerFilePath] string thisFile = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
        return Path.Combine(repositoryRoot, "src", "WindowsSetupAssistant.App", "WindowsSetupAssistant.App.csproj");
    }

    [Fact]
    public void SatelliteResourceLanguagesKeepsAllThreeLanguages()
    {
        // Bay im lang: de nguyen "en" thi ban publish mat sach ban dich ma khong bao loi.
        var value = XDocument.Load(ProjectFile())
            .Descendants("SatelliteResourceLanguages")
            .Select(e => e.Value)
            .SingleOrDefault();

        Assert.NotNull(value);
        foreach (var code in new[] { "en", "vi", "zh-Hant" })
        {
            Assert.Contains(code, value!.Split(';', StringSplitOptions.TrimEntries));
        }
    }

    [Fact]
    public void AppFontFamilyHasChineseFallback([CallerFilePath] string thisFile = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
        var controls = Path.Combine(repositoryRoot, "src", "WindowsSetupAssistant.App", "Themes", "Controls.xaml");

        var text = File.ReadAllText(controls);

        // Segoe UI khong co glyph chu Han - phai co font du phong.
        Assert.Contains("JhengHei", text, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Bước 2: Chạy test để chắc chắn nó thất bại**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~PublishConfiguration"
```

Kết quả mong đợi: **2 test FAIL** — `SatelliteResourceLanguages` đang là `en`, `Controls.xaml`
chưa có font dự phòng.

- [ ] **Bước 3: Sửa csproj**

Đổi dòng hiện có:

```xml
    <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
```

thành:

```xml
    <SatelliteResourceLanguages>en;vi;zh-Hant</SatelliteResourceLanguages>
```

- [ ] **Bước 4: Sửa font**

Trong `Themes/Controls.xaml`, đổi:

```xml
    <FontFamily x:Key="AppFontFamily">Segoe UI</FontFamily>
```

thành:

```xml
    <!-- Segoe UI không có glyph chữ Hán; WPF tự lùi sang font sau cho từng ký tự. -->
    <FontFamily x:Key="AppFontFamily">Segoe UI, Microsoft JhengHei UI, Microsoft JhengHei, PMingLiU</FontFamily>
```

- [ ] **Bước 5: Khởi tạo ngôn ngữ lúc chạy**

Trong `App.xaml.cs`, ngay sau khi đọc `settings` và **trước khi** tạo bất cứ ViewModel nào:

```csharp
        var language = LanguageCatalog.Resolve(settings.Language, CultureInfo.InstalledUICulture);
        LocalizationSource.Instance.SetLanguage(language);
        settings.Language = language.Name;
        settingsStore.Save(settings);
```

Thêm using: `System.Globalization;`, `WindowsSetupAssistant.App.Localization;`.

- [ ] **Bước 6: Thêm bộ chọn ngôn ngữ vào `MainViewModel`**

Thêm thuộc tính:

```csharp
    public IReadOnlyList<LanguageOption> Languages => LanguageCatalog.Supported;

    private LanguageOption _selectedLanguage = LanguageCatalog.Supported[0];

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value) || value is null)
            {
                return;
            }

            LocalizationSource.Instance.SetLanguage(CultureInfo.GetCultureInfo(value.Code));
            _settings.Language = value.Code;
            _settingsStore.Save(_settings);

            // Chuỗi tính toán trong ViewModel không tự đổi như binding trong XAML,
            // nên báo cho WPF biết mọi thuộc tính đều đã thay đổi.
            OnPropertyChanged(string.Empty);
        }
    }
```

Trong constructor, đặt giá trị ban đầu **không** kích hoạt setter (gán thẳng field):

```csharp
        _selectedLanguage = LanguageCatalog.Supported
            .FirstOrDefault(l => string.Equals(l.Code, settings.Language, StringComparison.OrdinalIgnoreCase))
            ?? LanguageCatalog.Supported[0];
```

- [ ] **Bước 7: Thêm ComboBox vào `MainWindow.xaml`**

Thêm `xmlns:loc="clr-namespace:WindowsSetupAssistant.App.Localization"` vào thẻ `Window`.
Trong `StackPanel` của thanh tiêu đề, ngay **trước** nút đổi giao diện:

```xml
                <TextBlock Text="{loc:Text Ui_LanguageLabel}" VerticalAlignment="Center"
                           Margin="0,0,8,0" Foreground="{DynamicResource MutedTextBrush}" />
                <ComboBox Width="130"
                          ItemsSource="{Binding Languages}"
                          SelectedItem="{Binding SelectedLanguage}"
                          DisplayMemberPath="DisplayName" />
                <Border Width="1" Background="{DynamicResource AppBorderBrush}" Margin="12,2" />
```

- [ ] **Bước 8: Chạy test**

```bash
dotnet build
dotnet test
```

Kết quả mong đợi: build 0 lỗi 0 cảnh báo; **205 test pass** (203 + 2).

- [ ] **Bước 9: Kiểm chứng bằng mắt**

```bash
dotnet publish src/WindowsSetupAssistant.App -c Release -r win-x64 -o publish-i18n
```

Kiểm tra thư mục `publish-i18n` **có** hai thư mục con `vi` và `zh-Hant` chứa satellite assembly.
Nếu không có thì bước 3 chưa ăn.

- [ ] **Bước 10: Commit**

```bash
git add src/WindowsSetupAssistant.App tests/WindowsSetupAssistant.App.Tests/Localization/PublishConfigurationTests.cs
git commit -m "Cau hinh publish da ngon ngu, font chu Han va bo chon ngon ngu"
```

---

## Task 6: Chuyển `MainWindow.xaml` sang khoá

Từ task này trở đi là công việc lặp lại: chuyển chuỗi cứng sang khoá. Quy trình giống nhau cho
mọi file, nên chỉ mô tả kỹ một lần ở đây.

**Files:**
- Modify: `src/WindowsSetupAssistant.App/Views/MainWindow.xaml`
- Modify: `src/WindowsSetupAssistant.App/Localization/UiKeys.cs`
- Modify: cả ba file `.resx`

**Interfaces:**
- Consumes: `TextExtension` (Task 3), `UiKeys` (Task 2)
- Produces: thêm hằng vào `UiKeys` cho mọi chuỗi của `MainWindow.xaml`

- [ ] **Bước 1: Liệt kê chuỗi cần chuyển**

```bash
grep -o 'Text="[^"{][^"]*"\|Content="[^"{][^"]*"\|Header="[^"{][^"]*"\|ToolTip="[^"{][^"]*"' src/WindowsSetupAssistant.App/Views/MainWindow.xaml
```

Kết quả: danh sách chuỗi cứng. Bỏ qua các chuỗi đã dùng `{Binding …}` hoặc `{loc:Text …}`.

- [ ] **Bước 2: Thêm hằng vào `UiKeys`**

Với mỗi chuỗi, thêm một hằng. Quy ước tên: `Ui_` + vùng + việc. Ví dụ mẫu:

```csharp
    // --- Thẻ Danh sách phần mềm ---
    public const string TabSoftwareList = "Ui_TabSoftwareList";
    public const string BtnSelectAll = "Ui_BtnSelectAll";
    public const string BtnSelectNone = "Ui_BtnSelectNone";
    public const string BtnInvertSelection = "Ui_BtnInvertSelection";
    public const string BtnSelectNotInstalled = "Ui_BtnSelectNotInstalled";
    public const string BtnAdd = "Ui_BtnAdd";
    public const string BtnEdit = "Ui_BtnEdit";
    public const string BtnDelete = "Ui_BtnDelete";
    public const string ColumnInstall = "Ui_ColumnInstall";
    public const string ColumnName = "Ui_ColumnName";
    public const string ColumnPackageId = "Ui_ColumnPackageId";
    public const string ColumnCategory = "Ui_ColumnCategory";
    public const string ColumnStatus = "Ui_ColumnStatus";
    public const string ColumnNotes = "Ui_ColumnNotes";
    public const string BtnCheckInstalled = "Ui_BtnCheckInstalled";
    public const string BtnRetryFailed = "Ui_BtnRetryFailed";
    public const string BtnCancelInstall = "Ui_BtnCancelInstall";
    public const string BtnStartInstall = "Ui_BtnStartInstall";
```

- [ ] **Bước 3: Thêm giá trị vào cả ba `.resx`**

Mỗi khoá thêm đúng một `<data>` vào **cả ba** file. Giá trị tiếng Việt lấy nguyên văn chuỗi đang có
trong XAML. Ví dụ cho `Ui_BtnStartInstall`:

| File | Giá trị |
|---|---|
| `Strings.resx` | `Start installing` |
| `Strings.vi.resx` | `Bắt đầu cài đặt` |
| `Strings.zh-Hant.resx` | `開始安裝` |

- [ ] **Bước 4: Đổi XAML**

```xml
<!-- trước -->
<Button Content="Bắt đầu cài đặt" Command="{Binding InstallSelectedCommand}" />

<!-- sau -->
<Button Content="{loc:Text Ui_BtnStartInstall}" Command="{Binding InstallSelectedCommand}" />
```

- [ ] **Bước 5: Chạy test đối chiếu — đây là lưới an toàn**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~ResourceParity"
```

Kết quả mong đợi: **PASS**. Nếu đỏ, thông báo lỗi sẽ chỉ đúng khoá nào thiếu ở ngôn ngữ nào.

- [ ] **Bước 6: Kiểm tra không còn chuỗi cứng**

```bash
grep -c 'Text="[^"{]\|Content="[^"{]\|Header="[^"{]\|ToolTip="[^"{]' src/WindowsSetupAssistant.App/Views/MainWindow.xaml
```

Kết quả mong đợi: **0**.

- [ ] **Bước 7: Chạy toàn bộ và commit**

```bash
dotnet build
dotnet test
git add src/WindowsSetupAssistant.App
git commit -m "Chuyen MainWindow.xaml sang khoa da ngon ngu"
```

---

## Task 7: Chuyển các cửa sổ còn lại sang khoá

**Files:**
- Modify: `src/WindowsSetupAssistant.App/Views/PackageEditorWindow.xaml`
- Modify: `src/WindowsSetupAssistant.App/Views/InstallConfirmWindow.xaml`
- Modify: `src/WindowsSetupAssistant.App/Views/TextInputWindow.xaml`
- Modify: `src/WindowsSetupAssistant.App/Views/ScanResultWindow.xaml`
- Modify: `src/WindowsSetupAssistant.App/Localization/UiKeys.cs` và ba file `.resx`

**Interfaces:**
- Consumes: giống Task 6
- Produces: hằng `UiKeys` cho toàn bộ cửa sổ phụ

- [ ] **Bước 1: Với TỪNG file trong danh sách trên, làm đủ 5 việc sau**

1. Liệt kê chuỗi cứng còn lại trong file:

```bash
grep -o 'Text="[^"{][^"]*"\|Content="[^"{][^"]*"\|Header="[^"{][^"]*"\|ToolTip="[^"{][^"]*"' <đường-dẫn-file>
```

2. Với mỗi chuỗi, thêm một hằng vào `UiKeys.cs` theo quy ước `Ui_` + vùng + việc, ví dụ:

```csharp
    // --- Hộp thoại Thêm/Sửa phần mềm ---
    public const string EditorTitleAdd = "Ui_EditorTitleAdd";
    public const string EditorTitleEdit = "Ui_EditorTitleEdit";
    public const string EditorFieldName = "Ui_EditorFieldName";
    public const string EditorFieldPackageId = "Ui_EditorFieldPackageId";
    public const string BtnSave = "Ui_BtnSave";
    public const string BtnCancel = "Ui_BtnCancel";
```

3. Thêm đúng một `<data>` cho mỗi khoá vào **cả ba** file `.resx`. Giá trị tiếng Việt lấy nguyên
   văn chuỗi đang có trong XAML. Ví dụ `Ui_BtnSave`:

| File | Giá trị |
|---|---|
| `Strings.resx` | `Save` |
| `Strings.vi.resx` | `Lưu` |
| `Strings.zh-Hant.resx` | `儲存` |

4. Đổi XAML sang markup extension, nhớ khai báo `xmlns:loc` trên thẻ `Window`:

```xml
<!-- trước -->
<Button Content="Lưu" Click="OnSaveClick" />

<!-- sau -->
<Button Content="{loc:Text Ui_BtnSave}" Click="OnSaveClick" />
```

5. Chạy test đối chiếu — nó sẽ chỉ đúng khoá nào thiếu ở ngôn ngữ nào:

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~ResourceParity"
```

Kết quả mong đợi: **PASS**.

- [ ] **Bước 2: Kiểm tra toàn bộ thư mục Views**

```bash
grep -rc 'Text="[^"{]\|Content="[^"{]\|Header="[^"{]\|ToolTip="[^"{]' --include="*.xaml" src/WindowsSetupAssistant.App/Views
```

Kết quả mong đợi: mọi file đều **0**.

- [ ] **Bước 3: Chạy toàn bộ và commit**

```bash
dotnet build
dotnet test
git add src/WindowsSetupAssistant.App
git commit -m "Chuyen cac cua so phu sang khoa da ngon ngu"
```

---

## Task 8: Chặn chuỗi cứng quay lại

Task này dựng lưới an toàn thứ hai: một test quét mã nguồn, bảo đảm chuỗi tiếng Việt không lẻn
trở lại các file đã chuyển.

**Files:**
- Test: `tests/WindowsSetupAssistant.App.Tests/Localization/NoHardcodedTextTests.cs`

**Interfaces:**
- Consumes: không có
- Produces: danh sách `NoHardcodedTextTests.ConvertedFiles` — các task sau bổ sung file vào đây
  sau khi chuyển xong

- [ ] **Bước 1: Viết test**

```csharp
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace WindowsSetupAssistant.App.Tests.Localization;

/// <summary>
/// Quét mã nguồn để bảo đảm chuỗi tiếng Việt không lẻn trở lại các file đã chuyển sang khoá.
/// Danh sách file được bổ sung dần theo tiến độ chuyển đổi.
/// </summary>
public class NoHardcodedTextTests
{
    /// <summary>Các file đã chuyển xong. Thêm dần khi hoàn thành từng task.</summary>
    public static readonly string[] ConvertedFiles =
    {
        @"src\WindowsSetupAssistant.App\Views\MainWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\PackageEditorWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\InstallConfirmWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\TextInputWindow.xaml",
        @"src\WindowsSetupAssistant.App\Views\ScanResultWindow.xaml"
    };

    private static readonly Regex VietnameseLetters = new(
        "[àáảãạăằắẳẵặâầấẩẫậđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));

    public static TheoryData<string> Files()
    {
        var data = new TheoryData<string>();
        foreach (var file in ConvertedFiles)
        {
            data.Add(file);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void ConvertedFileHasNoVietnameseOutsideComments(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot(), relativePath);
        Assert.True(File.Exists(path), $"Không tìm thấy {path}");

        var offending = new List<string>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            var trimmed = line.TrimStart();

            // Chú thích được phép viết tiếng Việt.
            if (trimmed.StartsWith("<!--", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("///", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            if (VietnameseLetters.IsMatch(line))
            {
                offending.Add($"dòng {lineNumber}: {trimmed}");
            }
        }

        Assert.True(offending.Count == 0,
            $"{relativePath} còn chuỗi tiếng Việt cứng:{Environment.NewLine}{string.Join(Environment.NewLine, offending)}");
    }
}
```

- [ ] **Bước 2: Chạy test**

```bash
dotnet test tests/WindowsSetupAssistant.App.Tests --filter "FullyQualifiedName~NoHardcodedText"
```

Kết quả mong đợi: **5 test PASS**. Nếu đỏ, thông báo chỉ rõ file và dòng còn sót — quay lại
Task 6 hoặc 7 xử lý nốt.

- [ ] **Bước 3: Commit**

```bash
git add tests/WindowsSetupAssistant.App.Tests/Localization/NoHardcodedTextTests.cs
git commit -m "Them test chan chuoi cung quay lai file da chuyen"
```

---

## Task 9: Chuyển ViewModel sang localizer

**Files:**
- Modify: `src/WindowsSetupAssistant.App/ViewModels/MainViewModel.cs`
- Modify: `src/WindowsSetupAssistant.App/ViewModels/SearchViewModel.cs`
- Modify: `src/WindowsSetupAssistant.App/ViewModels/SoftwarePackageViewModel.cs`
- Modify: `src/WindowsSetupAssistant.App/ViewModels/ScanResultViewModel.cs`
- Modify: `src/WindowsSetupAssistant.App/ViewModels/PackageEditorViewModel.cs`
- Modify: `src/WindowsSetupAssistant.App/ViewModels/InstallConfirmViewModel.cs`
- Modify: `src/WindowsSetupAssistant.App/ViewModels/LogViewModel.cs`
- Modify: `tests/WindowsSetupAssistant.App.Tests/ViewModelFixture.cs`
- Modify: `tests/WindowsSetupAssistant.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `IStringLocalizer` (Task 2), `UiKeys` (Task 2)
- Produces: mọi ViewModel nhận `IStringLocalizer` qua constructor

- [ ] **Bước 1: Tiêm localizer vào ViewModel**

Mẫu áp dụng cho từng ViewModel — thêm tham số constructor **cuối cùng** để ít ảnh hưởng thứ tự:

```csharp
    private readonly IStringLocalizer _localizer;

    // … trong constructor
    _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
```

`ViewModelFixture` truyền `LocalizationSource.Instance.Localizer`.

- [ ] **Bước 2: Đổi chuỗi cứng thành tra khoá**

```csharp
// trước
StatusMessage = "Đang kiểm tra WinGet...";

// sau
StatusMessage = _localizer[UiKeys.StatusCheckingWinget];
```

Với chuỗi có tham số:

```csharp
// trước
StatusMessage = $"Đã thêm {package.Name}.";

// sau
StatusMessage = _localizer.Format(LocalizedText.Of(UiKeys.StatusPackageAdded, package.Name));
```

Thêm hằng vào `UiKeys` và giá trị vào ba `.resx` cho từng chuỗi, đúng quy trình Task 6.

- [ ] **Bước 3: Bổ sung file vào danh sách canh chuỗi cứng**

Thêm 7 file ViewModel vào `NoHardcodedTextTests.ConvertedFiles`.

- [ ] **Bước 4: Cập nhật test đang so khớp chuỗi tiếng Việt**

Ví dụ trong `MainViewModelTests`:

```csharp
// trước
Assert.Contains(fixture.Dialogs.Messages, m => m.Contains("Không tìm thấy phần mềm nào"));

// sau
Assert.Contains(fixture.Dialogs.Messages, m => m.Contains(fixture.Localizer[UiKeys.DialogNoSoftwareFound]));
```

`ViewModelFixture` expose thêm:

```csharp
    public IStringLocalizer Localizer => LocalizationSource.Instance.Localizer;
```

- [ ] **Bước 5: Chạy toàn bộ và commit**

```bash
dotnet build
dotnet test
git add src tests
git commit -m "Chuyen ViewModel sang dung localizer"
```

Kết quả mong đợi: build 0 lỗi 0 cảnh báo; toàn bộ test xanh (số test tăng theo số file thêm vào
`ConvertedFiles`).

---

## Task 10: Domain và Application trả `LocalizedText`

**Files:**
- Modify: `src/WindowsSetupAssistant.Domain/Validation/PackageIdValidator.cs`
- Modify: `src/WindowsSetupAssistant.Domain/Validation/SearchQueryValidator.cs`
- Modify: `src/WindowsSetupAssistant.Domain/Models/InstallationResult.cs`
- Modify: `src/WindowsSetupAssistant.Domain/Models/LogEntry.cs`
- Modify: `src/WindowsSetupAssistant.Application/Abstractions/IAppLogger.cs`
- Modify: `src/WindowsSetupAssistant.Application/Services/InstallationQueueService.cs`
- Modify: `src/WindowsSetupAssistant.Application/Services/MachineScanService.cs`
- Modify: các file test liên quan

**Interfaces:**
- Consumes: `LocalizedText`, `MessageKeys`, `LocalizedException` (Task 1)
- Produces: `InstallationResult.Message` và `LogEntry.Message` kiểu `LocalizedText`;
  `IAppLogger.Information/Warning/Error(LocalizedText, …)`;
  `PackageIdValidator.TryValidate(string?, out LocalizedText)`

- [ ] **Bước 1: Đổi validator**

```csharp
    public static bool TryValidate(string? packageId, out LocalizedText error)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            error = LocalizedText.Of(MessageKeys.PackageIdEmpty);
            return false;
        }

        packageId = packageId.Trim();

        if (packageId.Length > MaxLength)
        {
            error = LocalizedText.Of(MessageKeys.PackageIdTooLong, MaxLength);
            return false;
        }

        if (packageId.StartsWith('-'))
        {
            error = LocalizedText.Of(MessageKeys.PackageIdStartsWithDash);
            return false;
        }

        if (!PackageIdPattern().IsMatch(packageId))
        {
            error = LocalizedText.Of(MessageKeys.PackageIdInvalidCharacters);
            return false;
        }

        error = LocalizedText.Raw(string.Empty);
        return true;
    }

    public static string EnsureValid(string? packageId)
    {
        if (!TryValidate(packageId, out var error))
        {
            throw new LocalizedException(error);
        }

        return packageId!.Trim();
    }
```

Làm tương tự cho `SearchQueryValidator`.

- [ ] **Bước 2: Đổi model và logger**

`InstallationResult.Message` và `LogEntry.Message` đổi kiểu sang `LocalizedText`, giá trị mặc định
`LocalizedText.Raw(string.Empty)`. **Xoá** `LogEntry.ToString()` — việc kết xuất chuyển sang
`AppLogger` ở Task 11.

`IAppLogger` đổi ba phương thức sang nhận `LocalizedText message`.

`AppLoggerExtensions.LogCommand` cũng đang dựng câu tiếng Việt - đổi thành:

```csharp
    public static void LogCommand(this IAppLogger logger, string command, int exitCode, string? details = null)
    {
        logger.Log(new LogEntry
        {
            Level = exitCode == 0 ? LogLevel.Information : LogLevel.Error,
            Message = LocalizedText.Of(exitCode == 0 ? MessageKeys.CommandSucceeded : MessageKeys.CommandFailed),
            Command = command,
            ExitCode = exitCode,
            Details = details
        });
    }
```

- [ ] **Bước 3: Đổi chuỗi trong service**

```csharp
// trước
_logger.Information($"Bắt đầu hàng đợi cài đặt: {ordered.Count} phần mềm. …");

// sau
_logger.Information(LocalizedText.Of(MessageKeys.QueueStarted, ordered.Count));
```

- [ ] **Bước 4: Cập nhật test**

```csharp
// trước
Assert.Contains("Mất kết nối mạng", failed.Message);

// sau
Assert.Equal(MessageKeys.UnexpectedError, failed.Message.Key);
Assert.Contains("Mất kết nối mạng", failed.Message.Arguments.Select(a => a?.ToString()));
```

`RecordingLogger` đổi theo chữ ký mới của `IAppLogger`.

- [ ] **Bước 5: Chạy toàn bộ và commit**

```bash
dotnet build
dotnet test
git add src tests
git commit -m "Domain va Application tra ve khoa thay vi cau chu"
```

---

## Task 11: Infrastructure trả `LocalizedText`, log file tiếng Anh

**Files:**
- Modify: `src/WindowsSetupAssistant.Infrastructure/Winget/WingetExitCodes.cs`
- Modify: `src/WindowsSetupAssistant.Infrastructure/Winget/WingetService.cs`
- Modify: `src/WindowsSetupAssistant.Infrastructure/Persistence/JsonProfileRepository.cs`
- Modify: `src/WindowsSetupAssistant.Infrastructure/Persistence/DefaultCatalogFactory.cs`
- Modify: `src/WindowsSetupAssistant.Infrastructure/Logging/AppLogger.cs`
- Modify: `src/WindowsSetupAssistant.App/App.xaml.cs` (thứ tự lắp ráp)
- Test: `tests/WindowsSetupAssistant.Tests/Logging/AppLoggerTests.cs`

**Interfaces:**
- Consumes: `IStringLocalizer` (Task 2), `MessageKeys` (Task 1)
- Produces: `WingetExitCodes.Describe(int) → LocalizedText`;
  `AppLogger(string? logDirectory, bool writeToFile, IStringLocalizer localizer)`;
  `DefaultCatalogFactory.Create(IStringLocalizer)`

- [ ] **Bước 1: Viết test cho log tiếng Anh**

```csharp
using System.Globalization;
using WindowsSetupAssistant.Domain.Localization;
using WindowsSetupAssistant.Infrastructure.Logging;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.Tests.Logging;

public class AppLoggerTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "wsa-log-" + Guid.NewGuid().ToString("N"));

    public AppLoggerTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FileLogIsAlwaysEnglishEvenWhenUiIsVietnamese()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");

        try
        {
            var localizer = new StubLocalizer();
            var logger = new AppLogger(_directory, writeToFile: true, localizer);

            logger.Information(LocalizedText.Of(MessageKeys.AppStarted));

            var text = File.ReadAllText(logger.LogFilePath!);

            // StubLocalizer tra ve "<culture>:<key>" nen kiem tra duoc culture da dung.
            Assert.Contains("en:" + MessageKeys.AppStarted, text);
            Assert.DoesNotContain("vi:" + MessageKeys.AppStarted, text);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
```

`StubLocalizer` thêm vào `tests/WindowsSetupAssistant.Tests/Fakes/StubLocalizer.cs`:

```csharp
using System.Globalization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Localization;

namespace WindowsSetupAssistant.Tests.Fakes;

/// <summary>Trả về "&lt;culture&gt;:&lt;key&gt;" để test kiểm chứng được đã dùng ngôn ngữ nào.</summary>
internal sealed class StubLocalizer : IStringLocalizer
{
    public string this[string key] => Format(LocalizedText.Of(key));

    public string Format(LocalizedText text) => Format(text, CultureInfo.CurrentUICulture);

    public string Format(LocalizedText text, CultureInfo culture) =>
        text.IsRaw ? text.Key : $"{culture.TwoLetterISOLanguageName}:{text.Key}";
}
```

- [ ] **Bước 2: Sửa `AppLogger`**

```csharp
    private static readonly CultureInfo FileLogCulture = CultureInfo.GetCultureInfo("en");

    private readonly IStringLocalizer _localizer;

    public AppLogger(string? logDirectory, bool writeToFile, IStringLocalizer localizer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        // … phần khởi tạo thư mục giữ nguyên
    }

    /// <summary>
    /// Kết xuất một dòng log để ghi file. LUÔN dùng tiếng Anh để file log chia sẻ được
    /// và tra cứu được, kể cả khi giao diện đang chạy tiếng Việt hay tiếng Trung.
    /// </summary>
    private string Render(LogEntry entry)
    {
        var parts = new List<string>
        {
            $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level.ToString().ToUpperInvariant()}] " +
            _localizer.Format(entry.Message, FileLogCulture)
        };

        if (!string.IsNullOrWhiteSpace(entry.Command))
        {
            parts.Add($"    > {entry.Command}");
        }

        if (entry.ExitCode.HasValue)
        {
            parts.Add($"    exit code: {entry.ExitCode.Value} (0x{entry.ExitCode.Value:X8})");
        }

        if (!string.IsNullOrWhiteSpace(entry.Details))
        {
            parts.Add("    " + entry.Details.Replace("\n", "\n    "));
        }

        return string.Join(Environment.NewLine, parts);
    }
```

`WriteToFile` gọi `Render(entry)` thay cho `entry.ToString()`.

- [ ] **Bước 3: Sửa `WingetExitCodes.Describe`**

Đổi kiểu trả về sang `LocalizedText`, mỗi nhánh `switch` trả một khoá:

```csharp
    public static LocalizedText Describe(int exitCode) => exitCode switch
    {
        Success => LocalizedText.Of(MessageKeys.ExitSuccess),
        NoApplicationsFound => LocalizedText.Of(MessageKeys.ExitNoApplicationsFound),
        PackageAlreadyInstalled or InstallAlreadyInstalled => LocalizedText.Of(MessageKeys.ExitAlreadyInstalled),
        // … một nhánh cho mỗi khoá Exit* đã khai báo ở Task 1
        _ => LocalizedText.Of(MessageKeys.ExitUnknown, exitCode)
    };
```

- [ ] **Bước 4: Sửa thứ tự lắp ráp trong `App.xaml.cs`**

`AppLogger` giờ cần localizer, nên phải tạo **sau** khi đã đặt ngôn ngữ:

```csharp
        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();

        var language = LanguageCatalog.Resolve(settings.Language, CultureInfo.InstalledUICulture);
        LocalizationSource.Instance.SetLanguage(language);
        settings.Language = language.Name;
        settingsStore.Save(settings);

        var localizer = LocalizationSource.Instance.Localizer;
        var logger = new AppLogger(logDirectory: null, writeToFile: true, localizer);
```

- [ ] **Bước 5: Sửa `DefaultCatalogFactory`**

Tên cấu hình mẫu là **dữ liệu**, chỉ sinh theo ngôn ngữ ở lần chạy đầu; file đã tồn tại giữ nguyên.

Thêm 6 khoá vào `MessageKeys` (đây là dữ liệu do tầng dưới sinh ra, không phải chuỗi giao diện):

```csharp
    public const string SeedProfilePersonal = "Msg_SeedProfilePersonal";
    public const string SeedProfilePersonalDescription = "Msg_SeedProfilePersonalDescription";
    public const string SeedProfileDeveloper = "Msg_SeedProfileDeveloper";
    public const string SeedProfileDeveloperDescription = "Msg_SeedProfileDeveloperDescription";
    public const string SeedProfileCompany = "Msg_SeedProfileCompany";
    public const string SeedProfileCompanyDescription = "Msg_SeedProfileCompanyDescription";
```

Đổi `DefaultCatalogFactory.Create()` thành nhận localizer:

```csharp
    public static SoftwareCatalog Create(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var personal = new InstallationProfile
        {
            Name = localizer[MessageKeys.SeedProfilePersonal],
            Description = localizer[MessageKeys.SeedProfilePersonalDescription],
            Packages = Order(new List<SoftwarePackage> { /* danh sách gói giữ nguyên như hiện tại */ })
        };

        // … hai cấu hình còn lại làm y hệt với khoá Developer và Company

        return new SoftwareCatalog
        {
            SchemaVersion = 1,
            ActiveProfileId = personal.Id,
            Profiles = new List<InstallationProfile> { personal, developer, company }
        };
    }
```

`JsonProfileRepository` nhận localizer qua constructor và truyền xuống ở hai chỗ đang gọi
`DefaultCatalogFactory.Create()` (lần chạy đầu và khi file hỏng). Tên phần mềm
(`Google Chrome`, `7-Zip`) **không** dịch.

- [ ] **Bước 6: Chạy toàn bộ và commit**

```bash
dotnet build
dotnet test
git add src tests
git commit -m "Infrastructure tra ve khoa, file log luon tieng Anh"
```

---

## Task 12: Cập nhật kịch bản kiểm thử giao diện

**Files:**
- Modify: `tools/ui-smoke-test/Run-UiSmokeTest.ps1`
- Modify: `tools/ui-smoke-test/README.md`

**Interfaces:**
- Consumes: `AppSettings.Language` (Task 4)
- Produces: kịch bản ghim ngôn ngữ `vi` trước khi chạy

- [ ] **Bước 1: Ghim ngôn ngữ trước khi khởi động app**

Kịch bản đang tìm nút theo nhãn tiếng Việt nên phải bảo đảm app chạy tiếng Việt. Thêm vào phần
chuẩn bị, **trước** lần `Start-Process` đầu tiên:

```powershell
# Kich ban tim nut theo nhan tieng Viet nen phai ghim ngon ngu, khong phu thuoc may chay.
$dataDir = Join-Path (Split-Path $appExe) "Data"
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
'{ "theme": "Light", "existingPackageAction": "Skip", "checkInstalledOnStartup": true, "language": "vi" }' |
    Out-File (Join-Path $dataDir "app-settings.json") -Encoding utf8
```

- [ ] **Bước 2: Ghi chú vào README của kịch bản**

Thêm mục giải thích vì sao phải ghim ngôn ngữ, để người sau không xoá nhầm.

- [ ] **Bước 3: Chạy kịch bản**

```bash
dotnet publish src/WindowsSetupAssistant.App -c Release -r win-x64 -o publish
cd tools/ui-smoke-test
dotnet build fake-winget/winget.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-UiSmokeTest.ps1
```

Kết quả mong đợi: `Dat: 16 | Truot: 0`.

- [ ] **Bước 4: Commit**

```bash
git add tools/ui-smoke-test
git commit -m "Kich ban kiem thu giao dien ghim ngon ngu tieng Viet"
```

---

## Kiểm thử thủ công cuối cùng

- [ ] Mở app, đổi ngôn ngữ sang **English** → mọi nhãn đổi ngay, **không** khởi động lại
- [ ] Đổi sang **繁體中文** → chữ Hán hiển thị đúng, không ra ô vuông
- [ ] Kiểm tra bảng Kết quả cài đặt và thẻ Nhật ký cũng đổi theo
- [ ] Đóng app, mở lại → vẫn giữ ngôn ngữ đã chọn
- [ ] Mở `Logs/*.log` → nội dung **tiếng Anh** dù giao diện đang tiếng Trung
- [ ] Xoá `Data/` rồi chạy lại trên máy Windows tiếng Anh → app tự chọn English, tên cấu hình mẫu tiếng Anh
- [ ] `publish/` có hai thư mục con `vi` và `zh-Hant`
- [ ] Chép bản publish sang máy khác, chạy thử cả 3 ngôn ngữ
