# Thiết kế: Quét và sao lưu phần mềm trước khi cài lại Windows

- **Ngày:** 07/09/2026
- **Trạng thái:** Đã chốt thiết kế, chờ viết kế hoạch triển khai
- **Dự án:** Windows Setup Assistant

## 1. Vấn đề

Khi cài lại Windows cho người dùng, kỹ thuật viên phải tự nhớ hoặc ghi tay xem máy cũ đang có
những phần mềm gì. Sau khi cài xong, việc tìm lại đúng bộ phần mềm đó tốn thời gian và dễ bỏ sót.

Công cụ hiện đã cài được hàng loạt phần mềm từ một danh sách có sẵn, nhưng danh sách đó phải do
người dùng tự dựng. Thiếu bước **lấy danh sách từ chính máy sắp bị cài lại**.

## 2. Mục tiêu

Thêm một chức năng: quét phần mềm đang có trên máy, cho kỹ thuật viên lọc bớt, rồi lưu thành

1. một **cấu hình cài đặt** dùng lại được ngay trong ứng dụng sau khi cài lại Windows, và
2. một **bộ file sao lưu** (JSON + CSV) mang theo USB.

### Ngoài phạm vi (cố tình không làm)

- Sao lưu file cài đặt (.exe/.msi) của phần mềm
- Sao lưu thiết lập, dữ liệu người dùng, khoá bản quyền, registry
- Hiển thị danh sách "cài tay" bên trong ứng dụng (chỉ xuất ra file; có thể làm sau)
- Cột nhà phát hành — xem mục 3.1

## 3. Quyết định thiết kế đã chốt

| Quyết định | Lựa chọn | Lý do |
|---|---|---|
| Phạm vi quét | Tách 2 nhóm: cài tự động được / phải cài tay | Nhóm 2 chính là thứ dễ bị bỏ sót nhất |
| Nơi cất | Vừa tạo cấu hình trong app, vừa xuất file | Cấu hình cho tốc độ; file cho an toàn khi app chạy từ ổ C |
| Lọc trước khi lưu | Có, hiện bảng cho tick chọn | Máy người dùng luôn có phần mềm không đáng mang sang |
| Xem danh sách cài tay | File CSV kèm bản sao lưu | Mở bằng Excel, in ra được; ít việc nhất |
| Nguồn dữ liệu | Chỉ `winget list` | Tái sử dụng parser đã kiểm chứng trên 190 dòng thật |

### 3.1. Giới hạn đã biết: không có cột nhà phát hành

`winget list` chỉ trả về 4 cột: **Name, Id, Version, Source**. Không có Publisher.

Muốn có nhà phát hành thì phải đọc registry `Uninstall` — đã cân nhắc và loại vì phần thu được
ít (`winget list` đã bao gồm mục Apps & Features) trong khi phải thêm một khối code mới kèm
nhiễu (bản vá KB, thành phần hệ thống). Người dùng đã xác nhận chấp nhận giới hạn này.

## 4. Kiến trúc

Bám đúng chiều phụ thuộc hiện có: `App → Infrastructure → Application → Domain`.

| Lớp | Thành phần mới | Ghi chú |
|---|---|---|
| Domain | `InstalledSoftwareEntry`, `InstalledSoftwareKind`, `MachineSnapshot`, `InstalledSoftwareClassifier` | Thuần dữ liệu và quy tắc, không phụ thuộc gì |
| Domain | `InstallationProfile.ManualSoftware` (thuộc tính mới) | Danh sách cài tay đi theo cấu hình |
| Application | `IMachineScanService`, `MachineScanService`, `IBackupExporter`, `BackupPaths` | Điều phối, không biết winget.exe hay định dạng file |
| Infrastructure | `BackupExporter`; sửa `WingetOutputParser` | Ghi file thật, phân tích text thật |
| App | `ScanResultViewModel`, `ScanResultWindow`, lệnh `ScanAndBackupCommand` | Giao diện |

## 5. Model dữ liệu

### 5.1. Domain

```csharp
public enum InstalledSoftwareKind
{
    WingetPackage,    // cài lại tự động được
    ManualOnly,       // phải cài tay
    SystemComponent   // app hệ thống, mặc định ẩn
}

public sealed record InstalledSoftwareEntry(
    string Name,
    string RawId,          // Id nguyên gốc từ winget list, ví dụ "ARP\Machine\X64\Android Studio"
    string Version,
    string? Source,        // "winget", "msstore" hoặc null
    InstalledSoftwareKind Kind);

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

`InstallationProfile` nhận thêm:

```csharp
public List<InstalledSoftwareEntry> ManualSoftware { get; set; } = new();
```

File JSON cũ không có trường này vẫn đọc được bình thường (System.Text.Json để nguyên giá trị
mặc định là danh sách rỗng). `CatalogNormalizer` chỉ cần bảo đảm không null, **không** áp
`PackageIdValidator` lên nhóm này vì `RawId` cố tình không phải Package Id hợp lệ.

### 5.2. Quy tắc phân loại

`InstalledSoftwareClassifier.Classify(WingetPackageInfo row)` — hàm thuần, xét theo thứ tự:

1. `RawId` bắt đầu bằng `MSIX\` (không phân biệt hoa thường) → `SystemComponent`
2. `Source` là `winget` hoặc `msstore` **và** `PackageIdValidator.IsValid(RawId)` → `WingetPackage`
3. Còn lại → `ManualOnly`

Tiền tố `ARP\` và `MSIX\` là định danh nội bộ của winget, **không bị dịch** theo ngôn ngữ Windows.

## 6. Sửa `WingetOutputParser`

Đây là thay đổi rủi ro nhất vì parser đang được 20 test và toàn bộ chức năng kiểm tra "đã cài"
sử dụng.

**Hiện tại:** `LooksLikePackageId` loại mọi Id chứa khoảng trắng. Bộ lọc này **có lý do chính đáng**:
nó chặn dòng tổng kết cuối bảng (`"2 upgrades available."`) bị cắt nhầm thành một dòng dữ liệu.

**Hệ quả không mong muốn:** nó cũng loại luôn `ARP\Machine\X64\Android Studio` — chính là nhóm
phần mềm cài tay mà chức năng này cần.

**Sửa tối thiểu:** chấp nhận thêm Id bắt đầu bằng `ARP\` hoặc `MSIX\`.

```csharp
private static bool LooksLikePackageId(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return false;

    // Mục Apps & Features / MSIX có Id chứa khoảng trắng nhưng vẫn là dòng dữ liệu thật.
    if (value.StartsWith(@"ARP\", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith(@"MSIX\", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (value.Any(char.IsWhiteSpace)) return false;
    return char.IsLetterOrDigit(value[0]) || value[0] == '{';
}
```

Dòng tổng kết vẫn bị loại vì ô Id của nó cắt ra là `"."` — không khớp nhánh nào.

**Ảnh hưởng tới chức năng cũ:** `GetInstalledPackagesAsync` sẽ trả thêm các dòng `ARP\`/`MSIX\`.
Việc kiểm tra "đã cài" so khớp **chính xác** theo Package Id nên các dòng thừa này vô hại.
Toàn bộ test parser hiện có phải tiếp tục xanh, không được sửa test cũ để lách.

## 7. Interface

```csharp
// Application/Abstractions
public interface IMachineScanService
{
    Task<MachineSnapshot> ScanAsync(CancellationToken cancellationToken = default);
}

public interface IBackupExporter
{
    // Ghi <jsonFilePath> và file CSV cùng thư mục, trả về đường dẫn cả hai.
    Task<BackupPaths> ExportAsync(
        InstallationProfile profile,
        string jsonFilePath,
        CancellationToken cancellationToken = default);
}

public sealed record BackupPaths(string JsonPath, string CsvPath);
```

`MachineScanService` phụ thuộc `IWingetService` + `IAppLogger`; gọi `GetInstalledPackagesAsync`,
phân loại qua `InstalledSoftwareClassifier`, dựng `MachineSnapshot` với
`MachineName = Environment.MachineName`.

## 8. Luồng hoạt động

1. Kỹ thuật viên bấm **"Quét & sao lưu máy này"** (thanh hành động dưới, cạnh "Kiểm tra đã cài").
2. `MachineScanService.ScanAsync` chạy `winget list` — bất đồng bộ, huỷ được, timeout 3 phút đã có sẵn.
3. Mở `ScanResultWindow`:
   - Tiêu đề: `Đã quét thấy N phần mềm trên DESKTOP-ABC`
   - Bảng 1: **Cài lại tự động được (N)** — cột: tick chọn, Tên, Package Id, Phiên bản. Tick sẵn toàn bộ.
   - Bảng 2: **Phải cài tay (N)** — cột: tick chọn, Tên, Phiên bản, Mã định danh. Tick sẵn toàn bộ.
   - Ô chọn **"Hiện cả app hệ thống (N)"** — bỏ tick là mặc định; khi tick, các mục
     `SystemComponent` được đưa vào bảng 2 (không tick sẵn).
   - Nút **Huỷ** / **Lưu bản sao lưu**.
4. Bấm **Lưu bản sao lưu**:
   - Tạo `InstallationProfile` tên `Sao lưu <TênMáy> <dd-MM-yyyy>`; trùng tên thì thêm ` (2)`, ` (3)`…
     - `Packages` ← các mục nhóm 1 đã tick (`SortOrder` đánh lại 0,1,2…)
     - `ManualSoftware` ← các mục nhóm 2 đã tick

   Ánh xạ từ `InstalledSoftwareEntry` sang `SoftwarePackage`:

   | Trường đích | Giá trị |
   |---|---|
   | `Name` | `entry.Name` |
   | `PackageId` | `entry.RawId` |
   | `Version` | `entry.Version` (chỉ để tham khảo, không ghim phiên bản khi cài) |
   | `Source` | `entry.Source` nếu nằm trong danh sách trắng, ngược lại `"winget"` |
   | `Category` | Đoán bằng heuristic `GuessCategory` đang nằm private trong `MainViewModel`; **tách ra thành helper dùng chung** cho cả hai nơi gọi. Không đoán được thì `Other`. |
   | `IsSelected` | `true` |
   - Thêm cấu hình vào catalog, lưu `Data/software-list.json`, chuyển sang cấu hình vừa tạo.
   - Mở hộp thoại chọn nơi lưu file (thư mục mặc định: thư mục chứa .exe, để mặc định rơi vào USB).
   - `BackupExporter` ghi 2 file.
   - Hiện thông báo kết quả kèm đường dẫn.

Nếu cả hai nhóm đều 0 mục đã tick → không tạo cấu hình, báo cho người dùng biết.
Nếu chỉ nhóm 2 có mục → vẫn tạo cấu hình (rỗng gói) để giữ `ManualSoftware`, và vẫn ghi file.

## 9. Định dạng file xuất

Tên file, `<TênMáy>` được làm sạch ký tự không hợp lệ:

- `sao-luu-<TênMáy>-<yyyyMMdd-HHmm>.json`
- `sao-luu-<TênMáy>-<yyyyMMdd-HHmm>-cai-tay.csv`

### 9.1. JSON

Là một `SoftwareCatalog` chứa **đúng một** cấu hình → nút **"Nhập JSON"** sẵn có khôi phục được
ngay, không cần code mới. Danh sách cài tay nằm trong `profiles[0].manualSoftware`.

`ActiveProfileId` phải trỏ đúng vào cấu hình đó, nếu không `CatalogNormalizer` sẽ tự sửa lúc nhập.
`SchemaVersion` giữ nguyên giá trị của catalog đang dùng.

### 9.2. CSV

- Mã hoá **UTF-8 có BOM** — thiếu BOM thì Excel hiển thị sai tiếng Việt.
- Dòng đầu là `sep=,` — chỉ dẫn để Excel tách cột đúng trên cả máy dùng dấu phẩy lẫn dấu chấm phẩy.
- Dòng thứ hai là tiêu đề: `STT,Tên phần mềm,Phiên bản,Mã định danh,Đã cài lại (x)`
- Cột cuối để trống cho kỹ thuật viên tick tay sau khi in.
- Escape đúng chuẩn RFC 4180: ô chứa `,` `"` hoặc xuống dòng thì bọc trong `"`, dấu `"` bên trong
  nhân đôi thành `""`.

## 10. Xử lý lỗi

| Tình huống | Hành vi |
|---|---|
| Máy chưa có WinGet | Nút bị khoá sẵn theo cơ chế `IsWingetAvailable` hiện có |
| `winget list` treo | Timeout 3 phút có sẵn → báo lỗi, không tạo cấu hình rỗng |
| `winget list` trả lỗi | Ghi nhật ký + báo người dùng, không tạo gì |
| Quét ra 0 mục | Báo "không tìm thấy phần mềm nào", không mở cửa sổ kết quả |
| Người dùng huỷ giữa chừng | `CancellationToken`, không để lại dữ liệu dở |
| Ghi file thất bại (rút USB, ổ đầy) | Báo lỗi rõ ràng; **cấu hình đã tạo trong app vẫn giữ nguyên** |
| Trùng tên cấu hình | Tự thêm hậu tố ` (2)`, ` (3)`… |

## 11. Kiểm thử

Không test nào được gọi winget thật hay ghi ra ngoài thư mục tạm.

| Vùng | Nội dung |
|---|---|
| `WingetOutputParser` | Giữ được dòng `ARP\`/`MSIX\`; **vẫn** loại dòng tổng kết; toàn bộ test cũ tiếp tục xanh |
| `InstalledSoftwareClassifier` | Đúng 3 nhóm; `msstore` xếp vào nhóm tự động; `MSIX\` được xét trước |
| `MachineScanService` | Dùng `FakeWingetService` sẵn có; lỗi và huỷ được xử lý đúng |
| `BackupExporter` | JSON roundtrip qua `ImportAsync`; CSV có BOM; escape đúng ô chứa dấu phẩy, dấu ngoặc kép, tiếng Việt |
| `InstallationProfile` / `CatalogNormalizer` | `ManualSoftware` không bị mất khi lưu/đọc; không bị `PackageIdValidator` loại |
| `ScanResultViewModel` | Tick chọn lọc đúng; đặt tên cấu hình đúng; trùng tên thêm hậu tố; 0 mục thì không tạo |

## 12. Tiêu chí hoàn thành

- Quét máy thật ra được hai nhóm hợp lý, không lẫn app hệ thống trong danh sách mặc định
- Cấu hình sao lưu cài lại được bằng đúng luồng cài đặt hiện có, không cần code riêng
- File JSON nhập lại được bằng nút "Nhập JSON" sẵn có
- File CSV mở bằng Excel hiển thị đúng tiếng Việt và đúng cột
- Toàn bộ test cũ vẫn xanh; `dotnet build` 0 lỗi 0 cảnh báo
