# Thiết kế: Hỗ trợ đa ngôn ngữ (Việt / Trung phồn thể / Anh)

- **Ngày:** 07/09/2026
- **Trạng thái:** Đã chốt thiết kế, chờ viết kế hoạch triển khai
- **Dự án:** Windows Setup Assistant

## 1. Vấn đề

Toàn bộ chuỗi hiển thị trong ứng dụng đang là văn bản tiếng Việt viết thẳng trong code.
Kỹ thuật viên không đọc được tiếng Việt không dùng được công cụ này.

Quét thực tế trên mã nguồn hiện tại:

| Nơi | Số chuỗi tiếng Việt (xấp xỉ) |
|---|---|
| `WindowsSetupAssistant.App` (ViewModel + code-behind) | ~185 |
| `WindowsSetupAssistant.Infrastructure` | ~73 (riêng `WingetExitCodes.Describe` đã 44 dòng) |
| `WindowsSetupAssistant.Application` | ~22 |
| `WindowsSetupAssistant.Domain` | ~17 |
| XAML (`Text=`, `Content=`, `Header=`, `ToolTip=`) | ~90 |
| **Tổng** | **~390 chuỗi → ~1170 bản dịch cho 3 ngôn ngữ** |

Đây cũng là dịp sửa một lỗi kiến trúc đang tồn tại: `WingetExitCodes.Describe()` nằm ở tầng
Infrastructure nhưng trả về **câu tiếng Việt cho người dùng đọc**. Tầng dưới không nên biết
ngôn ngữ nào.

## 2. Mục tiêu

Ứng dụng chạy đầy đủ bằng **tiếng Việt**, **tiếng Trung phồn thể** và **tiếng Anh**, đổi ngôn ngữ
ngay trong lúc chạy mà không cần khởi động lại.

### Ngoài phạm vi

- Dịch README, tài liệu hướng dẫn `.docx`, spec và kế hoạch trong `docs/`
- Dịch nội dung do WinGet in ra (tên phần mềm, output của winget.exe)
- Giao diện phải-sang-trái (RTL)
- Thêm ngôn ngữ thứ tư

## 3. Quyết định đã chốt

| Quyết định | Lựa chọn |
|---|---|
| Phạm vi | Dịch mọi thứ người dùng nhìn thấy; tầng dưới trả **khoá** thay vì câu chữ |
| Cơ chế | `.resx` chuẩn .NET, mỗi ngôn ngữ một file |
| Đổi ngôn ngữ | Tức thì, không khởi động lại |
| File log trên đĩa | **Luôn tiếng Anh**, để chia sẻ và tra cứu khi hỗ trợ |
| Định dạng số/ngày | Đặt **cả** `CurrentCulture` lẫn `CurrentUICulture` theo ngôn ngữ đã chọn |
| Tên cấu hình mẫu | Sinh theo ngôn ngữ ở **lần chạy đầu tiên**; file đã có thì giữ nguyên |

### 3.1. Mã ngôn ngữ

`en`, `vi`, `zh-Hant`. Dùng `zh-Hant` (culture trung tính) thay vì `zh-TW` để bao cả Đài Loan,
Hồng Kông và Ma Cao.

## 4. Kiến trúc

### 4.1. Chiều phụ thuộc giữ nguyên

`App → Infrastructure → Application → Domain`. Không tầng nào phụ thuộc ngược lên.

| Tầng | Vai trò mới |
|---|---|
| Domain | `LocalizedText`, `MessageKeys` — thuần dữ liệu, không biết dịch thuật là gì |
| Application | `IStringLocalizer` — hợp đồng dịch |
| Infrastructure | Trả `LocalizedText` thay vì câu chữ; `AppLogger` ghi file bằng bản tiếng Anh |
| App | `ResourceStringLocalizer` (hiện thực `.resx`), `LocalizationSource`, markup extension, bộ chọn ngôn ngữ |

### 4.2. Khoá thay cho câu chữ

Các khối code dưới đây là **phác thảo hình dạng** để chốt thiết kế; code đầy đủ nằm ở kế hoạch
triển khai.

```csharp
// Domain/Localization/LocalizedText.cs
public sealed class LocalizedText
{
    public string Key { get; }
    public IReadOnlyList<object?> Arguments { get; }

    private LocalizedText(string key, object?[] arguments) { … }

    public static LocalizedText Of(string key, params object?[] arguments);

    /// <summary>Chuỗi không cần dịch (tên phần mềm, đường dẫn, output của winget).</summary>
    public static LocalizedText Raw(string text);
}
```

`Raw` là lối thoát bắt buộc phải có: nhiều thông điệp chứa nguyên văn output của winget hoặc
đường dẫn file — những thứ không dịch được và không nên dịch.

```csharp
// Domain/Localization/MessageKeys.cs — nơi duy nhất liệt kê khoá, gõ sai là lỗi biên dịch
public static class MessageKeys
{
    public const string InstallSucceeded = "Msg_InstallSucceeded";
    public const string WingetNotResponding = "Msg_WingetNotResponding";
    // … một hằng cho mỗi thông điệp của tầng dưới
}
```

Ngoại lệ cũng mang thông điệp cho người dùng đọc (`WingetService.SearchAsync` ném
`TimeoutException`, `PackageIdValidator.EnsureValid` ném `ArgumentException`), nên cần một kiểu
ngoại lệ mang được khoá:

```csharp
// Domain/Localization/LocalizedException.cs
public class LocalizedException : Exception
{
    public LocalizedText LocalizedMessage { get; }

    // Message của Exception giữ nguyên khoá - đủ dùng cho log kỹ thuật và debug.
    public LocalizedException(LocalizedText message) : base(message.Key) { … }
}
```

Mọi chỗ đang ném ngoại lệ với câu tiếng Việt sẽ đổi sang ném `LocalizedException`. Nơi bắt ngoại
lệ để hiển thị (`SearchViewModel`, `MainViewModel`) kiểm tra kiểu: là `LocalizedException` thì dịch
`LocalizedMessage`, không phải thì dùng `ex.Message` như cũ.

```csharp
// Application/Abstractions/IStringLocalizer.cs
public interface IStringLocalizer
{
    /// <summary>Lấy chuỗi theo khoá; khoá không tồn tại thì trả về chính khoá đó.</summary>
    string this[string key] { get; }

    /// <summary>Dịch và điền tham số theo culture đang dùng.</summary>
    string Format(LocalizedText text);

    /// <summary>Dịch theo một culture chỉ định — dùng để ghi file log luôn bằng tiếng Anh.</summary>
    string Format(LocalizedText text, CultureInfo culture);
}
```

### 4.3. Thay đổi kiểu dữ liệu

| Chỗ | Trước | Sau |
|---|---|---|
| `InstallationResult.Message` | `string` | `LocalizedText` |
| `LogEntry.Message` | `string` | `LocalizedText` |
| `WingetExitCodes.Describe(int)` | `string` | `LocalizedText` |
| `IAppLogger.Information/Warning/Error` | `string message` | `LocalizedText message` |
| `PackageIdValidator.TryValidate(out string error)` | `out string` | `out LocalizedText` |
| `SearchQueryValidator.TryValidate(out string error)` | `out string` | `out LocalizedText` |
| `AppLoggerExtensions.LogCommand` | dựng `Message` tiếng Việt | dựng `Message` bằng khoá |
| `PackageIdValidator.EnsureValid` | ném `ArgumentException` | ném `LocalizedException` |
| `SearchQueryValidator.EnsureValid` | ném `ArgumentException` | ném `LocalizedException` |
| `WingetService.SearchAsync` | ném `TimeoutException` / `InvalidOperationException` với câu tiếng Việt | ném `LocalizedException` |

`LogEntry.Command`, `LogEntry.Details` giữ nguyên `string?` — chúng chứa câu lệnh và output thô,
không dịch.

`LogEntry.ToString()` hiện đang dựng chuỗi để ghi file. Bỏ nó đi; việc kết xuất chuyển sang
`AppLogger` vì nó cần localizer để dịch sang tiếng Anh.

## 5. Đổi ngôn ngữ tức thì

Có hai lớp và cần phân biệt rõ vai trò để không lẫn:

- `ResourceStringLocalizer` — hiện thực `IStringLocalizer`, đọc `.resx`. Đây là **nguồn sự thật**,
  được tiêm vào ViewModel và `AppLogger`.
- `LocalizationSource` — lớp bọc singleton **chỉ phục vụ XAML binding**, uỷ quyền mọi việc tra cứu
  cho `ResourceStringLocalizer` và chịu trách nhiệm bắn `PropertyChanged` khi đổi ngôn ngữ.

Dùng lại đúng thủ thuật `ThemeManager` đang dùng để tráo Light/Dark.

```csharp
// App/Localization/LocalizationSource.cs
public sealed class LocalizationSource : INotifyPropertyChanged
{
    public static LocalizationSource Instance { get; }
    public string this[string key] => …;

    public void SetLanguage(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
```

Bắn `PropertyChanged` với tên `"Item[]"` làm **mọi binding tới indexer** cập nhật cùng lúc.

Markup extension để dùng trong XAML:

```xml
<Button Content="{loc:Text Btn_Install}" />
```

trả về `new Binding($"[{key}]") { Source = LocalizationSource.Instance, Mode = OneWay }`.

ViewModel không dùng được markup extension nên khi đổi ngôn ngữ sẽ gọi
`OnPropertyChanged(string.Empty)` — WPF hiểu đây là "mọi thuộc tính đã đổi" và vẽ lại toàn bộ.

## 6. Ba việc dễ bị bỏ sót

### 6.1. `SatelliteResourceLanguages`

`WindowsSetupAssistant.App.csproj` đang có:

```xml
<SatelliteResourceLanguages>en</SatelliteResourceLanguages>
```

Dòng này **loại bỏ mọi satellite assembly khác tiếng Anh** lúc publish. Không sửa thì bản publish
mất sạch tiếng Việt và tiếng Trung mà không báo lỗi gì. Đổi thành:

```xml
<SatelliteResourceLanguages>en;vi;zh-Hant</SatelliteResourceLanguages>
```

### 6.2. Font chữ Hán

`Themes/Controls.xaml` đang khai báo:

```xml
<FontFamily x:Key="AppFontFamily">Segoe UI</FontFamily>
```

Segoe UI **không có glyph chữ Hán**. Đổi thành chuỗi dự phòng:

```xml
<FontFamily x:Key="AppFontFamily">Segoe UI, Microsoft JhengHei UI, Microsoft JhengHei, PMingLiU</FontFamily>
```

WPF tự chọn font đầu tiên có glyph cho từng ký tự.

### 6.3. Định dạng số và ngày

Đặt cả `CurrentCulture` lẫn `CurrentUICulture` để giao diện tiếng Anh không hiện `12,3 seconds`
kiểu Việt. Đã kiểm tra các chỗ nhạy cảm và chúng **đều an toàn** vì đã ghim `InvariantCulture`
từ trước: `WingetService.ParseVersion`, số thứ tự trong CSV xuất bản sao lưu. System.Text.Json
luôn dùng invariant cho số nên file dữ liệu không bị ảnh hưởng.

## 7. Thiết lập và lần chạy đầu

`AppSettings` (hiện có `Theme`, `ExistingPackageAction`, `CheckInstalledOnStartup`) nhận thêm:

```csharp
public string Language { get; set; } = string.Empty;   // rỗng = chưa chọn, tự dò
```

Lần chạy đầu, dò theo `CultureInfo.InstalledUICulture`:

| Ngôn ngữ Windows | Chọn |
|---|---|
| Bắt đầu bằng `vi` | `vi` |
| Bắt đầu bằng `zh` | `zh-Hant` |
| Còn lại | `en` |

Ghi lựa chọn xuống `Data/app-settings.json` ngay khi người dùng đổi, giống cách `Theme` đang làm.

Bộ chọn ngôn ngữ là một `ComboBox` 3 mục đặt cạnh nút đổi giao diện Sáng/Tối trên thanh tiêu đề.

## 8. Dữ liệu người dùng

Tên cấu hình mẫu ("Máy cá nhân", "Máy lập trình", "Máy công ty") là **dữ liệu**, không phải giao diện.

- `DefaultCatalogFactory.Create()` nhận thêm `IStringLocalizer` và sinh tên theo ngôn ngữ đang chọn.
- File `software-list.json` **đã tồn tại thì giữ nguyên**, không dịch lại — vì người dùng đổi tên
  được, dịch đè sẽ xoá mất thứ họ tự đặt.

Tên phần mềm (`Google Chrome`, `7-Zip`) không dịch.

## 9. Nhật ký

Cùng một `LogEntry`, hai cách kết xuất:

| Đích | Ngôn ngữ |
|---|---|
| File `Logs/*.log` | **Luôn tiếng Anh** (`CultureInfo.GetCultureInfo("en")`) |
| Thẻ Nhật ký trên màn hình | Theo ngôn ngữ đang chọn, đổi tức thì |

`AppLogger` nhận `IStringLocalizer` qua constructor để dịch sang tiếng Anh khi ghi file.
Đây là lý do `IStringLocalizer` phải khai báo ở tầng Application chứ không phải App.

## 10. Xử lý lỗi

| Tình huống | Hành vi |
|---|---|
| Khoá không có trong `.resx` | Trả về **chính khoá đó** (ví dụ `Msg_Unknown`) — dễ thấy khi kiểm thử, không làm sập app |
| Thiếu satellite assembly lúc chạy | .NET tự lùi về ngôn ngữ mặc định (`en`) |
| `Language` trong settings không hợp lệ | Bỏ qua, dò lại theo Windows |
| Tham số `string.Format` không khớp số lượng | Bắt `FormatException`, trả chuỗi chưa điền tham số, ghi cảnh báo |
| Máy thiếu font chữ Hán | WPF tự lùi theo danh sách dự phòng ở mục 6.2 |

## 11. Kiểm thử

Hiện có **105 `[Fact]`/`[Theory]`** (145 test case). Khoảng **30+ test đang so khớp chuỗi tiếng Việt**
(ví dụ `Assert.Contains("không phản hồi", result.Message)`) sẽ đổi sang so khớp **khoá**:
`Assert.Equal(MessageKeys.WingetNotResponding, result.Message.Key)`.

Đây là thay đổi hành vi hợp lệ vì kiểu dữ liệu đã đổi — **không phải sửa test để lách**.

Test mới, xếp theo giá trị:

| Test | Vì sao quan trọng |
|---|---|
| **Đối chiếu tập khoá giữa 3 file `.resx`** | Thiếu một bản dịch ở bất kỳ ngôn ngữ nào là test đỏ ngay. Với 1170 chuỗi, đây là thứ giữ dự án không rơi rớt âm thầm |
| **Mọi hằng trong `MessageKeys` đều có trong `.resx`** | Khoá khai báo mà quên dịch sẽ hiện ra dạng `Msg_Xxx` trên màn hình |
| **Không `.resx` nào còn giá trị rỗng** | Bắt trường hợp tạo khoá nhưng để trống |
| `LocalizedText.Of` / `Raw` | Giữ đúng khoá và tham số |
| `ResourceStringLocalizer` | Khoá thiếu trả về chính khoá; `Format` điền tham số đúng theo culture |
| `LocalizationSource.SetLanguage` | Bắn `PropertyChanged("Item[]")`; đặt cả 4 thuộc tính culture |
| Dò ngôn ngữ lần đầu | `vi-VN`→`vi`, `zh-TW`/`zh-HK`→`zh-Hant`, `fr-FR`→`en` |
| `AppLogger` ghi file | Luôn tiếng Anh dù giao diện đang là ngôn ngữ khác |

Toàn bộ test cũ không liên quan tới chuỗi phải giữ nguyên và tiếp tục xanh.

## 12. Quy mô và thứ tự

Ước tính **12–14 task**. Thứ tự xếp sao cho **sau khoảng task 8 thì giao diện đã chạy đủ 3 ngôn ngữ**;
các task sau mới đổi tầng dưới sang trả khoá. Nhờ vậy có lợi ích của việc chia giai đoạn mà không
phải tách thành hai spec.

## 13. Tiêu chí hoàn thành

- Đổi ngôn ngữ trong lúc chạy, mọi chuỗi đổi ngay, không khởi động lại
- Tiếng Trung phồn thể hiển thị đúng chữ, không ra ô vuông
- Bản publish self-contained giữ đủ 3 ngôn ngữ
- File log vẫn tiếng Anh khi giao diện đang là tiếng Việt hoặc tiếng Trung
- Test đối chiếu khoá giữa 3 `.resx` xanh
- `dotnet build` 0 lỗi 0 cảnh báo; toàn bộ test xanh
- Kịch bản `tools/ui-smoke-test` vẫn chạy được (nhãn nút trong kịch bản phải đổi theo, xem mục 14)

## 14. Ảnh hưởng tới kịch bản kiểm thử giao diện

`tools/ui-smoke-test/Run-UiSmokeTest.ps1` đang tìm nút theo **nhãn tiếng Việt**
(`"Bắt đầu cài đặt"`, `"Huỷ cài đặt"`) và tiêu đề cửa sổ (`"Xác nhận cài đặt"`, `"Đang cài đặt"`).

Đổi ngôn ngữ sẽ làm kịch bản này hỏng. Cách xử lý: kịch bản nhận thêm tham số `-Language vi`
và ứng dụng đọc `Data/app-settings.json` như bình thường — kịch bản ghi sẵn file thiết lập với
`"language": "vi"` trước khi chạy, để nhãn luôn là tiếng Việt đúng như hiện tại.
