# Windows Setup Assistant

Ứng dụng Windows (WPF, .NET 8) giúp **cài hàng loạt phần mềm bằng WinGet** sau khi cài lại Windows.

Toàn bộ danh sách phần mềm nằm trong `Data/software-list.json` **cạnh file .exe**, nên bạn có thể chép cả
thư mục vào USB / ổ cứng ngoài / OneDrive và dùng lại trên máy vừa cài Windows mà không mất dữ liệu.

---

## 1. Tính năng

| Nhóm | Chức năng |
|------|-----------|
| Danh sách | Xem tên, WinGet Package Id, nhóm, ô chọn, trạng thái cài đặt |
| Quản lý | Thêm / sửa / xoá / sắp xếp thứ tự (↑ ↓) phần mềm |
| Tìm kiếm | Tra cứu trực tiếp trong kho WinGet và thêm vào danh sách |
| Cấu hình | Nhiều bộ cài: "Máy cá nhân", "Máy lập trình", "Máy công ty"... (tạo / đổi tên / nhân bản / xoá) |
| Nhóm | Trình duyệt, Lập trình, Văn phòng, Giải trí, Tiện ích, Khác |
| Chọn | Chọn 1 / nhiều / tất cả / đảo chọn / chỉ những cái chưa cài |
| Cài đặt | Cài **lần lượt**, có tiến trình, huỷ giữa chừng, một gói lỗi không làm dừng hàng đợi |
| Gói đã có | Cho chọn **Bỏ qua** hoặc **Nâng cấp** |
| Nhật ký | Câu lệnh, thời điểm, exit code, thông báo lỗi + ghi ra file `Logs/` |
| Dữ liệu | Import / Export JSON, mở thư mục dữ liệu |
| Giao diện | Dark Mode / Light Mode, hộp xác nhận trước khi cài hàng loạt |
| WinGet | Kiểm tra lúc khởi động, nếu thiếu thì hướng dẫn cài Microsoft App Installer |

---

## 2. Yêu cầu

- Windows 10 (1809+) hoặc Windows 11, **64-bit**
- **WinGet** (nằm trong ứng dụng *App Installer* của Microsoft Store)
  - Kiểm tra bằng cách mở Terminal và gõ: `winget --version`
  - Nếu chưa có: cài **App Installer** từ Microsoft Store (ứng dụng có sẵn nút mở thẳng trang này)
- Bản publish là **self-contained** nên máy đích **không cần cài .NET**

Để tự build: cần **.NET SDK 8.0** trở lên.

---

## 3. Cấu trúc dự án

```
WindowsSetupAssistant/
├─ WindowsSetupAssistant.sln
├─ global.json                     # ghim SDK .NET 8
├─ Directory.Build.props
├─ src/
│  ├─ WindowsSetupAssistant.Domain/          # Tầng lõi - không phụ thuộc gì cả
│  │  ├─ Entities/        SoftwarePackage, InstallationProfile, SoftwareCatalog
│  │  ├─ Models/          InstallationResult, WingetPackageInfo, LogEntry
│  │  ├─ Enums/           SoftwareCategory, InstallState, InstallOutcome, ...
│  │  └─ Validation/      PackageIdValidator, SearchQueryValidator
│  │
│  ├─ WindowsSetupAssistant.Application/     # Nghiệp vụ - chỉ phụ thuộc Domain
│  │  ├─ Abstractions/    IWingetService, IProfileRepository, IAppLogger, IProcessRunner
│  │  ├─ Models/          InstallationOptions, InstallationProgressUpdate, ...
│  │  └─ Services/        InstallationQueueService
│  │
│  ├─ WindowsSetupAssistant.Infrastructure/  # Chi tiết kỹ thuật
│  │  ├─ Winget/          WingetService, WingetOutputParser, WingetExitCodes, ProcessRunner
│  │  ├─ Persistence/     JsonProfileRepository, CatalogNormalizer, DefaultCatalogFactory
│  │  └─ Logging/         AppLogger
│  │
│  └─ WindowsSetupAssistant.App/             # Presentation (WPF, MVVM)
│     ├─ Mvvm/            ObservableObject, RelayCommand, AsyncRelayCommand
│     ├─ ViewModels/      MainViewModel, SearchViewModel, LogViewModel, ...
│     ├─ Views/           MainWindow, PackageEditorWindow, InstallConfirmWindow, TextInputWindow
│     ├─ Themes/          Light.xaml, Dark.xaml, Controls.xaml
│     ├─ Converters/      AppConverters.cs
│     └─ Services/        DialogService, ThemeManager, SettingsStore
│
└─ tests/
   ├─ WindowsSetupAssistant.Tests/           # 152 unit test (Domain/Application/Infrastructure), KHÔNG cài phần mềm thật
   └─ WindowsSetupAssistant.App.Tests/       # 141 unit test (ViewModel, binding, CLI, đa ngôn ngữ)
```

**Chiều phụ thuộc:** `App → Infrastructure → Application → Domain`.
Domain không biết gì về WinGet, JSON hay WPF, nên rất dễ test và dễ thay đổi về sau.

---

## 4. Build và chạy

```bash
cd WindowsSetupAssistant
dotnet build
```

```bash
dotnet run --project src/WindowsSetupAssistant.App
```

Chạy toàn bộ unit test:

```bash
dotnet test
```

---

## 5. Publish bản self-contained win-x64 (mang đi USB)

```bash
dotnet publish src/WindowsSetupAssistant.App -c Release -r win-x64 -o publish
```

Kết quả: đúng **một file** `publish/WindowsSetupAssistant.exe` (~63 MB, đã nén, đã nhúng sẵn .NET runtime).

Lần đầu chạy, ứng dụng tự tạo thêm hai thư mục **cạnh file exe**:

```
WindowsSetupAssistant.exe
Data/
  software-list.json      <- danh sách phần mềm của bạn
  app-settings.json       <- Dark/Light mode, tuỳ chọn khi gói đã tồn tại
Logs/
  setup-assistant-YYYYMMDD.log
```

> **Mang sang máy khác:** chép nguyên **cả thư mục** (exe + `Data/`) vào USB.
> Chỉ chép mỗi file .exe thì danh sách sẽ bị tạo lại từ mẫu mặc định.

Kiểm tra bản build không phụ thuộc .NET của máy:

```powershell
$env:DOTNET_ROOT = "C:\khong-ton-tai"
.\publish\WindowsSetupAssistant.exe
```

Nếu ứng dụng vẫn mở bình thường thì bản publish thật sự self-contained.

---

## 6. Cách dùng

### Cài phần mềm
1. Chọn **cấu hình** ở góc trên bên phải (ví dụ "Máy lập trình").
2. Bấm **Kiểm tra đã cài** để biết máy đang có sẵn gì (ứng dụng cũng tự làm việc này lúc khởi động).
3. Tick chọn phần mềm — hoặc bấm **Chỉ cái chưa cài**.
4. Bấm **Bắt đầu cài đặt** → hộp xác nhận hiện ra, chọn **Bỏ qua** hay **Nâng cấp** với gói đã có.
5. Theo dõi tiến trình; muốn dừng thì bấm **Huỷ cài đặt**.
6. Xong: xem tab **Kết quả cài đặt**; nếu có lỗi, bấm **Thử lại phần lỗi**.

### Thêm phần mềm mới (không cần sửa source code)
- Tab **Tìm trên WinGet** → gõ từ khoá → **Thêm vào danh sách**; hoặc
- Tab **Danh sách phần mềm** → **Thêm** → tự nhập Package Id.

Lấy Package Id chính xác bằng lệnh:

```bash
winget search "tên phần mềm" --source winget
```

### Import / Export
- **Xuất tất cả** – lưu toàn bộ cấu hình ra một file JSON.
- **Xuất cấu hình này** – chỉ xuất cấu hình đang chọn (tiện để chia sẻ cho đồng nghiệp).
- **Nhập JSON** – chọn *Yes* để thay thế toàn bộ, *No* để thêm vào danh sách hiện có.

### Định dạng file `Data/software-list.json`

```json
{
  "schemaVersion": 1,
  "activeProfileId": "11995361-e76f-4ece-b3af-e193c389b737",
  "profiles": [
    {
      "id": "11995361-e76f-4ece-b3af-e193c389b737",
      "name": "Máy cá nhân",
      "description": "Bộ phần mềm cơ bản cho máy dùng hằng ngày.",
      "packages": [
        {
          "id": "af97c9df-ba44-4677-9fcf-d5d56dab61c9",
          "name": "Google Chrome",
          "packageId": "Google.Chrome",
          "category": "Browser",
          "source": "winget",
          "isSelected": true,
          "sortOrder": 0
        }
      ]
    }
  ]
}
```

`category` nhận: `Browser`, `Development`, `Office`, `Entertainment`, `Utility`, `Other`.
Sửa tay file này cũng được — mục nào có `packageId` sai định dạng sẽ tự bị loại và ghi cảnh báo vào nhật ký.

---

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

---

## 8. Danh sách mẫu (Package Id đã kiểm chứng bằng `winget search`)

| Phần mềm | WinGet Package Id | Nhóm |
|----------|-------------------|------|
| Google Chrome | `Google.Chrome` | Trình duyệt |
| Mozilla Firefox | `Mozilla.Firefox` | Trình duyệt |
| Visual Studio Code | `Microsoft.VisualStudioCode` | Lập trình |
| Git | `Git.Git` | Lập trình |
| Node.js | `OpenJS.NodeJS` | Lập trình |
| 7-Zip | `7zip.7zip` | Tiện ích |
| VLC media player | `VideoLAN.VLC` | Giải trí |
| Notepad++ | `Notepad++.Notepad++` | Tiện ích |

---

## 9. An toàn và quyền

- **Không ghép chuỗi lệnh.** Mọi tham số đi qua `ProcessStartInfo.ArgumentList`, không qua `cmd.exe`
  (`UseShellExecute = false`), nên các ký tự như `& | > ;` hoàn toàn vô hại.
- **Package Id luôn được kiểm tra** bằng `PackageIdValidator` trước khi thực thi; Id bắt đầu bằng `-`
  hay chứa khoảng trắng, xuống dòng, ký tự lạ đều bị từ chối.
- **Nguồn nằm trong danh sách trắng** (`winget`, `msstore`); giá trị khác tự động bị ép về `winget`.
- **Không tự tải file EXE/MSI** từ bất kỳ website nào — mọi thứ do WinGet tải từ nguồn chính thức.
- **Không lưu mật khẩu / token**; file JSON chỉ chứa tên và Package Id.
- **Không yêu cầu Administrator** khi khởi động (chạy ở mức `asInvoker`). Chỉ khi bạn tự bấm
  **"Chạy bằng quyền Admin"** thì ứng dụng mới khởi động lại qua UAC.
- **Không thay đổi thiết lập Windows** và không cài thêm thành phần nào ngoài các gói bạn chọn.

Câu lệnh thực tế được sinh ra:

```
winget install --id <PACKAGE_ID> --exact --source winget --silent --accept-package-agreements --accept-source-agreements [--disable-interactivity]
```

`--disable-interactivity` chỉ được thêm khi phát hiện WinGet ≥ 1.4, để không lỗi trên máy Windows 10
còn dùng App Installer đời cũ.

---

## 10. Xử lý tình huống bất thường

| Tình huống | Ứng dụng làm gì |
|------------|-----------------|
| Không có WinGet | Hiện banner cảnh báo + nút mở Microsoft Store, khoá nút cài đặt |
| Mất mạng | Ghi lỗi cho gói đó và **chạy tiếp** gói sau |
| WinGet treo | Timeout (30 phút/gói) rồi kill cả cây tiến trình, báo lỗi |
| Package Id không tồn tại | Exit code `0x8A150014` → "Không tìm thấy gói này trong nguồn WinGet" |
| Người dùng bấm Huỷ | Dừng gói hiện tại, các gói còn lại đánh dấu "Đã huỷ" |
| Thiếu quyền Admin | Exit code `0x8A150019` → gợi ý chạy lại bằng quyền Administrator |
| File JSON hỏng | Đổi tên thành `.corrupted-<thời điểm>.bak`, tạo lại danh sách mẫu, ghi log |
| Đóng app khi đang cài | Hỏi xác nhận trước khi thoát |

---

## 11. Kiểm thử

```bash
dotnet test
```

293 unit test (152 trong `WindowsSetupAssistant.Tests` + 141 trong `WindowsSetupAssistant.App.Tests`), chia theo 4 nhóm chính đúng yêu cầu dự án:

- **Xử lý JSON** – `JsonProfileRepositoryTests`, `CatalogNormalizerTests`
  (roundtrip, seed lần đầu, file hỏng, import/export, loại bỏ Package Id không hợp lệ)
- **Phân tích kết quả WinGet** – `WingetOutputParserTests`, `WingetServiceTests`
  (đọc bảng text thật của winget, ánh xạ exit code, dựng câu lệnh)
- **Hàng đợi cài đặt** – `InstallationQueueServiceTests`
  (đúng thứ tự, bỏ qua/nâng cấp, huỷ giữa chừng, tiến trình)
- **Xử lý lỗi** – một gói lỗi không làm dừng hàng đợi, exception được ghi lại, Id sai không chạy winget

`IWingetService` luôn được thay bằng bản giả (`FakeWingetService`) nên **test không cài phần mềm thật**.

---

## 12. Checklist kiểm thử thực tế trên máy vừa cài lại Windows

Chép thư mục publish vào USB rồi làm lần lượt:

- [ ] Cắm USB, chạy `WindowsSetupAssistant.exe` — mở được, **không** báo thiếu .NET
- [ ] Thanh trạng thái hiện đúng đường dẫn `Data/software-list.json` trên USB
- [ ] Danh sách phần mềm hiện đúng như trên máy cũ (không bị mất dữ liệu)
- [ ] Nếu máy chưa có WinGet: banner cảnh báo hiện ra, nút **Mở Microsoft Store** hoạt động
- [ ] Sau khi cài App Installer và mở lại: banner biến mất, thanh trạng thái báo phiên bản WinGet
- [ ] Bấm **Kiểm tra đã cài**: trạng thái từng dòng chuyển đúng "Đã cài" / "Chưa cài"
- [ ] Đổi **Giao diện Tối / Sáng**: toàn bộ cửa sổ đổi màu, tắt mở app vẫn nhớ lựa chọn
- [ ] Tab **Tìm trên WinGet**: tìm "chrome" ra kết quả, **Thêm vào danh sách** hoạt động
- [ ] Thêm / sửa / xoá / sắp xếp phần mềm → tắt app, mở lại thấy dữ liệu vẫn còn
- [ ] Nhập Package Id sai (ví dụ `--force`) → bị chặn kèm thông báo rõ ràng
- [ ] Chọn 2–3 phần mềm nhẹ (7-Zip, Notepad++, VLC) → **Bắt đầu cài đặt**
- [ ] Hộp xác nhận hiện đúng số lượng và cho chọn Bỏ qua / Nâng cấp
- [ ] Trong lúc cài: cửa sổ **không bị đơ**, tiến trình chạy, tên gói hiện tại đổi liên tục
- [ ] Bấm **Huỷ cài đặt** giữa chừng → dừng lại, gói còn lại ghi "Đã huỷ"
- [ ] Cài lại một gói đã có với tuỳ chọn **Bỏ qua** → kết quả "Bỏ qua (đã có)", không tải lại
- [ ] Thêm một Package Id không tồn tại → gói đó **Thất bại** nhưng các gói sau **vẫn được cài**
- [ ] Tab **Nhật ký**: thấy đầy đủ câu lệnh, thời điểm, exit code, thông báo lỗi
- [ ] File log được tạo trong thư mục `Logs/`
- [ ] Bấm **Thử lại phần lỗi** → chỉ chạy lại đúng những gói bị lỗi
- [ ] **Xuất tất cả** ra file JSON, xoá vài mục, **Nhập JSON** lại → dữ liệu khôi phục đúng
- [ ] Tạo cấu hình mới, đổi tên, nhân bản, xoá → đều lưu ngay xuống file
- [ ] Rút USB rồi cắm sang máy khác → danh sách vẫn nguyên vẹn

---

## 13. Ghi chú kỹ thuật đáng nhớ

- **Vì sao có `WpfApplication`?** Namespace `WindowsSetupAssistant.Application` (tầng nghiệp vụ) trùng tên
  với `System.Windows.Application` của WPF. File `GlobalUsings.cs` khai báo bí danh toàn cục
  `global using WpfApplication = System.Windows.Application;` để tránh nhầm lẫn.
- **Vì sao không dùng `Split(' ')` khi đọc kết quả WinGet?** Tên phần mềm, cột `Match` và cả số phiên bản
  (`"17.00 beta"`) đều có thể chứa khoảng trắng. Parser đọc vị trí cột từ dòng tiêu đề rồi cắt theo vị trí,
  và dùng **thứ tự cột** thay vì so khớp chữ "Name"/"Id" để không phụ thuộc ngôn ngữ Windows.
- **Vì sao giao diện không đơ?** Mọi lệnh gọi WinGet đều `async/await` với `CancellationToken`;
  tiến trình gửi về UI qua `IProgress<T>` (tạo trên UI thread nên callback tự chạy đúng luồng);
  log từ luồng nền được đưa về UI bằng `Dispatcher`.
- **Vì sao cài lần lượt?** Nhiều trình cài đặt Windows (MSI) không chạy song song được — chạy tuần tự
  tránh lỗi `0x8A150102 (Another installation is already in progress)`.
