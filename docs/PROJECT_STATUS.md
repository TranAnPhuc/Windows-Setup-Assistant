# Trạng thái dự án — Windows Setup Assistant

_Cập nhật: 2026-09-07 — Milestone 4: triển khai nền tảng quét và sao lưu phần mềm; publish Release đã xác minh khởi động._

Tài liệu Word hướng dẫn sử dụng đã bổ sung quy trình quét, rà soát, xuất JSON/CSV và khôi phục trên máy mới.

## Milestone 4 — Quét và sao lưu

Đã hoàn thành parser giữ lại các dòng `ARP\`/`MSIX\`, model phân loại Domain, dịch vụ quét qua `IWingetService`, exporter JSON/CSV UTF-8 BOM, ViewModel/cửa sổ xem lại và nút “Quét & sao lưu máy này”. Cấu hình vẫn giữ nguyên kiến trúc hiện tại; constructor cũ của `MainViewModel` được giữ qua overload để không phá test/consumer hiện có.

Kiểm thử mới dùng fake WinGet và thư mục tạm, không chạy cài đặt thật. Hiện build/test đạt **161/161** (144 core + 17 WPF).

## Chức năng đã hoàn thành

- Ứng dụng WPF .NET 8 theo MVVM, tách 4 tầng `Domain → Application → Infrastructure → App`.
- Tìm kiếm WinGet, thêm/sửa/xoá/sắp xếp phần mềm, chọn nhiều gói và lọc theo nhóm.
- Lưu tự động danh sách động tại `Data/software-list.json`; có ba profile mẫu: Máy cá nhân, Máy lập trình và Máy công ty.
- Tạo, đổi tên, nhân bản, xoá profile; import/export toàn bộ hoặc profile hiện tại bằng JSON.
- Kiểm tra gói đã cài, hàng đợi cài tuần tự, tiến trình, tổng kết kết quả, retry gói lỗi, log ra UI và `Logs/`.
- Huỷ cài đặt bằng `CancellationToken`; WinGet chạy bất đồng bộ qua `ProcessStartInfo.ArgumentList`.
- Tự động huỷ và đợi tiến trình con WinGet thoát hoàn toàn (`KillProcessTree` có chờ `WaitForExit`) trước khi kết thúc tác vụ.
- Đồng bộ vòng đời đóng ứng dụng (`PrepareForCloseAsync`): tự động dừng WinGet đang chạy, huỷ tìm kiếm, lưu trữ dữ liệu an toàn và giải phóng tài nguyên mà không chặn UI thread.
- Xử lý lỗi JSON an toàn: khi file JSON bị hỏng cú pháp, ứng dụng giữ nguyên file cũ, báo lỗi rõ ràng và tuyệt đối không tự ghi đè bằng dữ liệu mẫu.
- Sửa triệt để lỗi hiển thị ComboBox profile: khai báo ItemTemplate rõ ràng, bổ sung `ToString()` trên `InstallationProfile` trả về tên profile.
- Bộ kiểm thử toàn diện: 161/161 test đạt (144 test logic tầng Core + 17 test WPF UI/binding/layout/themes/icon).
- Đã publish bản cuối self-contained/single-file `win-x64`: `publish-final/WindowsSetupAssistant.exe` (62.9 MB) với icon ứng dụng nhúng đầy đủ (16px đến 256px).

## Quyết định kỹ thuật quan trọng

- `InstallationQueueService` clone toàn bộ gói ngay lúc bắt đầu hàng đợi. Vì vậy các thay đổi UI sau khi người dùng xác nhận không thể đổi Package Id hoặc thứ tự của những gói chưa chạy.
- Khi hàng đợi đang chạy, UI khoá các thao tác làm thay đổi danh sách/profile/import-export, cũng như tìm/thêm từ WinGet và chạy lại với quyền Administrator.
- WinGet chỉ nhận đối số qua `ArgumentList`; Package Id và source được xác thực/whitelist trước khi thực thi.
- `ProgressBar.Value` chỉ nhận dữ liệu từ ViewModel, nên Binding có `Mode=OneWay`.
- ComboBox cấu hình sử dụng `ItemTemplate` với `TextBlock Text="{Binding Name}"` thay vì `DisplayMemberPath`, đồng thời `InstallationProfile.ToString()` trả về `Name` nhằm đảm bảo tính tương thích và hiển thị chính xác ở mọi ngữ cảnh.
- Cần chạy MSBuild với một node trong worktree hiện tại (`-m:1 -p:UseSharedCompilation=false`).

## Kiểm chứng Milestone 3 (Bản cuối)

- `dotnet restore WindowsSetupAssistant.sln` — thành công.
- `dotnet build WindowsSetupAssistant.sln -m:1 -p:UseSharedCompilation=false` — thành công, 0 warning, 0 error.
- `dotnet test WindowsSetupAssistant.sln -m:1 -p:UseSharedCompilation=false --logger "console;verbosity=minimal"` — 161/161 passed (144 Core + 17 WPF UI).
- Đã chạy kiểm tra khởi động EXE Debug: chạy trơn tru, không có lỗi Binding.
- Đã publish Release self-contained/single-file `win-x64` vào `publish-final/WindowsSetupAssistant.exe`:
  - Kích thước: 65,965,190 byte (~62.9 MB).
  - SHA-256: `B894799FCA3C1AFADE9E30D7D0F6613D835DE8A3E67D5459C6B46788BC3D60FB`.
  - Icon: Đã trích xuất và xác thực icon 32x32 / multi-res embedded trong EXE.
  - Smoke test chạy thử `publish-final/WindowsSetupAssistant.exe`: khởi động thành công và đóng sạch sẽ.
  - Các bản `publish/` và `publish-fixed/` cũ được bảo lưu nguyên vẹn.

### File thay đổi chính trong đợt này

- `src/WindowsSetupAssistant.App/App.xaml.cs` — đồng bộ `OnStartup` và `OnExit` với `PrepareForCloseAsync()`.
- `src/WindowsSetupAssistant.App/Views/MainWindow.xaml.cs` — loại bỏ prompt trùng lặp, chuyển quyền đóng sang ViewModel.
- `src/WindowsSetupAssistant.App/ViewModels/MainViewModel.cs` — cập nhật trạng thái các nút khi đóng ứng dụng (`!_isClosing`), điều kiện `SelectedCount > 0` cho cài đặt.
- `src/WindowsSetupAssistant.Infrastructure/Persistence/JsonProfileRepository.cs` — giữ nguyên file cũ khi gặp JSON lỗi, không ghi đè dữ liệu mẫu.
- `src/WindowsSetupAssistant.Infrastructure/Winget/ProcessRunner.cs` — `KillProcessTree` bổ sung `WaitForExit(5000)` đảm bảo tiến trình đã thoát.
- `src/WindowsSetupAssistant.Domain/Classification/InstalledSoftwareClassifier.cs` — ưu tiên `MSIX\` là app hệ thống, nguồn WinGet hợp lệ mới cài tự động, còn lại ghi chú cài tay.
- `src/WindowsSetupAssistant.Infrastructure/Persistence/BackupExporter.cs` — xuất JSON một profile và CSV cài tay với BOM/RFC4180.
- `src/WindowsSetupAssistant.App/ViewModels/ScanResultViewModel.cs`, `src/WindowsSetupAssistant.App/Views/ScanResultWindow.xaml` — xem lại, tick chọn và tạo profile sao lưu.
- `src/WindowsSetupAssistant.Domain/Entities/InstallationProfile.cs` — override `ToString() => Name`.
- `src/WindowsSetupAssistant.App/Views/MainWindow.xaml` — bổ sung `Icon`, cập nhật `ItemTemplate` cho ComboBox profile, binding `IsEnabled` cho CheckBox phần mềm.
- `src/WindowsSetupAssistant.App/Services/ThemeManager.cs` — chuẩn hoá URI resource theme theo pack format.
- `tests/WindowsSetupAssistant.App.Tests/` — hoàn thiện bộ kiểm thử WPF (WpfTestHost, test binding, icon, layout DPI, regression test).
- `tests/WindowsSetupAssistant.Tests/` — bổ sung test huỷ tiến trình và kiểm tra giữ nguyên file JSON lỗi.

## Việc còn lại / Milestone tiếp theo

- Bổ sung test WPF trực tiếp cho `ScanResultWindow` và test ViewModel `ScanAndBackup` với fake dialog/exporter.
- Hoàn thiện hiển thị/lọc nhóm app hệ thống trong cửa sổ xem lại theo spec (hiện dữ liệu hệ thống được phân loại và không đưa vào profile mặc định).
- Chạy smoke test UI với fake-winget, khởi động bản Release, sau đó publish lại self-contained `win-x64` vào `publish-final` mới.

## Lệnh build/test

`dotnet restore WindowsSetupAssistant.sln --force -m:1`

`dotnet build WindowsSetupAssistant.sln --no-restore -m:1 -p:UseSharedCompilation=false`

`dotnet test WindowsSetupAssistant.sln --no-build --no-restore -m:1 --logger "console;verbosity=minimal"`

