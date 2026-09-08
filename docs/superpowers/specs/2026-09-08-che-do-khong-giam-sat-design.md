# Chế độ không giám sát (Unattended Mode) — Thiết kế

**Ngày:** 2026-09-08
**Trạng thái:** Chờ duyệt
**Dự án:** Windows Setup Assistant

---

## 1. Vấn đề

Nhân viên IT Helpdesk cài lại Windows cho nhiều máy trong cùng một buổi. Với mỗi máy họ phải:
mở ứng dụng, đợi quét, chọn cấu hình, tick phần mềm, bấm cài, ngồi canh, đóng ứng dụng.

Phần thao tác tay đó lặp lại y hệt trên mọi máy nhưng không tự động hoá được, vì ứng dụng
hiện chỉ có một đường vào duy nhất là giao diện đồ hoạ: [`App.OnStartup`](../../../src/WindowsSetupAssistant.App/App.xaml.cs)
không đọc `e.Args`, nên mọi tham số dòng lệnh đều bị bỏ qua.

**Mục tiêu:** cho phép chạy trọn một lượt cài đặt bằng đúng một dòng lệnh, không cần ai
ngồi trước máy, và trả kết quả về dạng script đọc được.

## 2. Phạm vi

### Trong phạm vi

- Tham số dòng lệnh bật chế độ không giám sát, chạy hoàn toàn không giao diện
- In tiến trình ra chính cửa sổ `cmd`/PowerShell đã gọi
- Mã thoát chi tiết cho script kiểm tra
- File báo cáo JSON liệt kê kết quả từng gói
- Huỷ giữa chừng bằng Ctrl+C
- `--help` mô tả cách dùng

### Ngoài phạm vi (cố ý)

- **Tự khởi động lại máy và chạy tiếp sau reboot.** Việc này cần ghi vào registry
  `RunOnce`, tức là thay đổi thiết lập Windows — tách thành hạng mục riêng để bàn kỹ.
  Lần này chỉ *ghi nhận* gói nào báo cần khởi động lại vào báo cáo.
- **Tự nâng quyền Administrator.** Nâng quyền làm hiện hộp thoại UAC, mà một hộp thoại
  chờ người bấm thì không còn là không giám sát nữa. Xem mục 8.
- Ghim phiên bản, chọn nguồn `msstore`, gỡ phần mềm, nâng cấp hàng loạt — là các thiếu sót
  riêng, không thuộc tính năng này.
- Tham số lọc theo nhóm (`--category`). Cấu hình đã là đơn vị chọn lọc; thêm bộ lọc thứ hai
  làm tăng tổ hợp phải kiểm thử mà chưa có nhu cầu thật.

## 3. Trải nghiệm mong muốn

```
C:\usb> WindowsSetupAssistant.exe --unattended --profile "May cong ty"

Windows Setup Assistant 1.1.0 - unattended mode
WinGet v1.9.25200 detected.
Profile "May cong ty": 8 package(s), 6 not installed.
[1/6] Google.Chrome ... OK (24.3s)
[2/6] Mozilla.Firefox ... OK (31.0s)
[3/6] Microsoft.VisualStudioCode ... OK (18.7s)
[4/6] Git.Git ... FAILED (exit 0x8A150011: no applicable installer)
[5/6] 7zip.7zip ... OK (5.1s)
[6/6] VideoLAN.VLC ... OK (12.9s)

Done in 1m 52s. 5 succeeded, 0 upgraded, 2 skipped, 1 failed.
Report: C:\usb\Reports\unattended-20260908-141233.json
Exit code 1 (one or more packages failed).

C:\usb> echo %ERRORLEVEL%
1
```

Không có cửa sổ nào mở ra. Máy để đó tự chạy.

## 4. Giao diện dòng lệnh

| Tham số | Bắt buộc | Mặc định | Ý nghĩa |
|---|---|---|---|
| `--unattended` | ✔ | — | Bật chế độ. Không có nó thì ứng dụng mở giao diện như cũ. |
| `--profile <tên>` | | Cấu hình đang chọn trong `software-list.json` | Tên cấu hình cần cài. So khớp không phân biệt hoa thường, bỏ khoảng trắng thừa. |
| `--existing skip\|upgrade` | | `skip` | Gói đã có trên máy: bỏ qua, hay nâng cấp. Khớp với `ExistingPackageAction`. |
| `--report <đường dẫn>` | | `Reports\unattended-<yyyyMMdd-HHmmss>.json` cạnh file .exe | Nơi ghi báo cáo. |
| `--help`, `-h`, `-?` | | — | In hướng dẫn rồi thoát với mã 0. |

**Quy tắc chọn gói:** lấy các gói **đang được tick** (`IsSelected == true`) trong cấu hình,
rồi bỏ những gói đã cài sẵn. Đây chính là hành vi của nút "Chỉ cái chưa cài" trong giao diện,
nên người dùng không phải học thêm khái niệm mới: những gì họ dựng sẵn trong giao diện là
những gì lệnh sẽ cài. Khi `--existing upgrade`, gói đã cài được nâng cấp thay vì bỏ qua.

**Xử lý tham số sai:** in thông báo lỗi ra console, in luôn phần `--help`, thoát mã 2.
Không đoán ý người dùng, không chạy tiếp với giá trị mặc định.

## 5. Mã thoát

| Mã | Ý nghĩa | Script nên làm gì |
|---|---|---|
| `0` | Mọi gói thành công (kể cả các gói bị bỏ qua vì đã có) | Sang máy tiếp theo |
| `1` | Chạy xong nhưng có ít nhất một gói lỗi | Đọc báo cáo, cài tay phần còn thiếu |
| `2` | Tham số sai, hoặc không tìm thấy cấu hình theo tên | Sửa lệnh |
| `3` | Không có WinGet trên máy | Cài App Installer trước |
| `4` | Bị huỷ bằng Ctrl+C | Chạy lại |

Chỉ năm mã này. Lỗi đọc dữ liệu được gộp vào mã 2, vì cấu hình cần cài do tham số chỉ định,
nên với người gõ lệnh thì "không có cấu hình đó" và "gõ sai tên cấu hình" là cùng một việc
phải sửa ở cùng một chỗ.

## 6. File báo cáo

Ghi **luôn luôn**, kể cả khi có gói lỗi hay bị huỷ — đó chính là lúc báo cáo có giá trị nhất.
Ngoài việc cho script đọc, file này còn dùng làm hồ sơ bàn giao máy.

```json
{
  "schemaVersion": 1,
  "startedAt": "2026-09-08T14:10:41+07:00",
  "finishedAt": "2026-09-08T14:12:33+07:00",
  "durationSeconds": 112.4,
  "machineName": "PC-KETOAN-03",
  "wingetVersion": "v1.9.25200",
  "profileName": "May cong ty",
  "existingPackageAction": "Skip",
  "exitCode": 1,
  "wasCancelled": false,
  "counts": {
    "total": 6, "succeeded": 5, "upgraded": 0,
    "skipped": 0, "failed": 1, "cancelled": 0
  },
  "packages": [
    {
      "name": "Google Chrome",
      "packageId": "Google.Chrome",
      "outcome": "Succeeded",
      "exitCode": 0,
      "durationSeconds": 24.3,
      "restartRequired": false,
      "errorMessage": null
    },
    {
      "name": "Git",
      "packageId": "Git.Git",
      "outcome": "Failed",
      "exitCode": -1978335215,
      "durationSeconds": 3.1,
      "restartRequired": false,
      "errorMessage": "No applicable installer found for this system."
    }
  ]
}
```

`restartRequired` là `true` khi WinGet trả mã báo cần khởi động lại. Lần này ứng dụng chỉ ghi
nhận, không tự reboot.

`schemaVersion` có mặt ngay từ đầu để về sau đổi định dạng mà không làm hỏng script cũ.

## 7. Ngôn ngữ của phần chữ ở chế độ này

**Console và báo cáo luôn là tiếng Anh**, không theo ngôn ngữ giao diện người dùng đã chọn.

Lý do giống hệt quyết định đã áp dụng cho file log: nội dung này do máy đọc và do kỹ thuật
viên khác đọc lại sau. Một script `if %ERRORLEVEL%` không quan tâm ngôn ngữ, nhưng một dòng
log lúc thì tiếng Việt lúc thì tiếng Trung thì không tra cứu được, và không thể dán lên
diễn đàn hỏi. Nhất quán quan trọng hơn thân thiện ở đây.

Hệ quả: các chuỗi này **không** vào file `.resx`, mà nằm trong một lớp hằng riêng. Chúng cũng
không làm hỏng bộ test chống chuỗi cứng hiện có, vì bộ test đó truy tìm chữ tiếng Việt.

## 8. Quyền Administrator

Chế độ không giám sát **không tự nâng quyền**. Nâng quyền sẽ bật hộp thoại UAC, mà một hộp
thoại đứng chờ người bấm thì phá vỡ đúng thứ tính năng này tồn tại để làm.

Thay vào đó: nếu cần cài ở phạm vi toàn máy, kỹ thuật viên mở sẵn PowerShell/cmd bằng quyền
Administrator rồi gõ lệnh — tiến trình con thừa hưởng quyền đó. Phần `--help` và tài liệu
hướng dẫn phải nói rõ điều này, vì đây là nguyên nhân số một khiến một lượt cài "chạy xong
mà chẳng thấy phần mềm đâu".

## 9. Kiến trúc

Giữ nguyên **một file .exe duy nhất**. Không tách thêm chương trình console riêng: yêu cầu
gốc của dự án là chép một thư mục vào USB là dùng được, thêm file thứ hai là thêm thứ để
quên khi chép.

### Đường đi khi khởi động

```
App.OnStartup(e)
  │
  ├─ CommandLineParser.Parse(e.Args)
  │
  ├─ Không có --unattended  ─────────────► dựng MainWindow như hiện tại (không đổi gì)
  │
  ├─ --help                 ─────────────► ConsoleSession in hướng dẫn → Shutdown(0)
  │
  ├─ Tham số sai            ─────────────► in lỗi + hướng dẫn → Shutdown(2)
  │
  └─ Hợp lệ ────► UnattendedRunner.RunAsync() ────► Shutdown(mã thoát)
                    (không bao giờ tạo Window nào)
```

Điểm phải cẩn thận: đặt `ShutdownMode = OnExplicitShutdown` trước khi chạy nhánh không giám
sát. Mặc định của WPF là đóng ứng dụng khi cửa sổ cuối cùng đóng lại — mà ở nhánh này không
có cửa sổ nào, nên nếu không đổi, tiến trình có thể tự thoát trước khi cài xong.

### File mới

| File | Trách nhiệm |
|---|---|
| `App/Cli/CommandLineOptions.cs` | Bản ghi bất biến chứa tham số đã phân tích |
| `App/Cli/CommandLineParser.cs` | Chuỗi tham số → `CommandLineParseResult` (Hợp lệ / Lỗi / Help). Hàm thuần, không I/O, dễ test |
| `App/Cli/ConsoleSession.cs` | Gắn vào console cha (`AttachConsole`), ghi dòng, nhả ra khi xong |
| `App/Cli/ConsoleMessages.cs` | Toàn bộ chuỗi tiếng Anh của chế độ này, gom một chỗ |
| `App/Cli/UnattendedRunner.cs` | Điều phối: kiểm tra WinGet → nạp dữ liệu → chọn cấu hình → lọc gói → chạy hàng đợi → ghi báo cáo → trả mã thoát |
| `App/Cli/UnattendedExitCode.cs` | Enum năm mã thoát |
| `App/Cli/UnattendedReport.cs` | Mô hình báo cáo + ghi JSON |

### Tái sử dụng, không viết lại

`UnattendedRunner` **không** tự gọi WinGet. Nó dùng đúng các thành phần giao diện đang dùng:
`WingetService`, `JsonProfileRepository`, `InstallationQueueService`, `AppLogger`. Nhờ vậy
hành vi cài đặt ở hai chế độ không thể lệch nhau — cùng một hàng đợi, cùng một câu lệnh
WinGet, cùng quy tắc "một gói lỗi không làm dừng hàng đợi".

Tiến trình hiển thị đi qua `IProgress<InstallationProgressUpdate>` sẵn có; chế độ này chỉ
thay chỗ nhận: thay vì đẩy lên thanh tiến trình thì in ra console.

### Vì sao `AttachConsole`

Ứng dụng là `WinExe` nên không có console riêng. `AttachConsole(-1)` mượn console của tiến
trình cha, tức là đúng cửa sổ `cmd` người dùng đang gõ. Đây là cách chuẩn cho ứng dụng GUI
cần nói chuyện với dòng lệnh, không cần thư viện ngoài.

Khi không có console cha (bấm đúp vào .exe, hoặc chạy từ Task Scheduler), `AttachConsole`
thất bại — lúc đó ứng dụng **vẫn chạy bình thường và vẫn ghi báo cáo**, chỉ là không in ra
đâu cả. Không được coi đây là lỗi.

## 10. Huỷ bằng Ctrl+C

Bắt `Console.CancelKeyPress`, đặt `e.Cancel = true` để Windows không giết tiến trình ngay,
rồi kích hoạt `CancellationToken` mà hàng đợi đang chờ. Gói đang cài dở được dừng theo đúng
cơ chế huỷ sẵn có, báo cáo vẫn được ghi, thoát mã 4.

Ctrl+C lần thứ hai thì để hệ điều hành xử lý theo mặc định — người dùng nhấn hai lần là đang
nói "dừng ngay bây giờ".

## 11. Các tình huống phải xử lý

| Tình huống | Hành vi |
|---|---|
| Không có WinGet | In hướng dẫn cài App Installer, thoát mã 3. Không cố cài gì. |
| Không tìm thấy cấu hình theo tên | In tên các cấu hình đang có để người dùng biết mình gõ sai chỗ nào, thoát mã 2. |
| Cấu hình không có gói nào chưa cài | In "nothing to do", ghi báo cáo rỗng, thoát mã 0. Đây là thành công, không phải lỗi. |
| File `software-list.json` hỏng | Repository sẵn có trả về danh sách mẫu; ứng dụng in cảnh báo rõ ràng rằng đang dùng dữ liệu mặc định để người dùng không tưởng nhầm là danh sách của mình. |
| Không ghi được file báo cáo | In đường dẫn và lý do ra console, **giữ nguyên** mã thoát của lượt cài. Không ghi được báo cáo không làm hỏng việc đã cài xong. |
| Mất mạng giữa chừng | Gói đó lỗi, hàng đợi chạy tiếp — hành vi sẵn có, không thêm gì. |
| Vừa `--unattended` vừa bấm đúp | Chạy im lặng, không có console để in, vẫn ghi báo cáo. |

## 12. An toàn

Ràng buộc của dự án được giữ nguyên, không nới:

- Tên cấu hình lấy từ tham số **chỉ dùng để so khớp trong bộ nhớ**, không bao giờ đi vào
  câu lệnh WinGet.
- Package Id vẫn đi qua `PackageIdValidator` và `ProcessStartInfo.ArgumentList` như cũ —
  chế độ mới không mở thêm đường nào cho dữ liệu người dùng chạm tới chuỗi lệnh.
- `--report` được chuẩn hoá bằng `Path.GetFullPath` và bọc trong `try/catch`; ghi thất bại
  thì báo, không làm sập.
- Không thêm thư viện NuGet nào.
- Không ghi registry, không đổi thiết lập Windows, không tự nâng quyền.

## 13. Kiểm thử

Nguyên tắc bất di bất dịch của dự án được giữ: **test không bao giờ cài phần mềm thật.**

**Test phân tích tham số** (`WindowsSetupAssistant.App.Tests`) — phần này thuần logic nên phủ dày:
thiếu `--unattended`, tên cấu hình có dấu và có khoảng trắng, `--existing` viết hoa/thường,
`--existing` giá trị lạ, tham số không biết, tham số thiếu giá trị đứng sau, `--help` lẫn với
tham số khác, chuỗi rỗng.

**Test `UnattendedRunner`** với `IWingetService` giả — kiểm chứng đúng thứ khó thấy bằng mắt:
mỗi mã thoát ứng với đúng một tình huống, gói đã cài bị bỏ qua khi `skip` và được nâng cấp khi
`upgrade`, một gói lỗi không làm dừng các gói sau, báo cáo được ghi cả khi lỗi lẫn khi huỷ,
ghi báo cáo thất bại không đổi mã thoát.

**Test báo cáo JSON** — khớp tên trường và giá trị `outcome`, vì đây là hợp đồng với script
bên ngoài: đổi tên một trường là làm hỏng script của người khác mà build vẫn xanh.

**Test đầu-cuối trên bản publish thật** — mở rộng `tools/ui-smoke-test` sẵn có, vốn đã có sẵn
một `winget.exe` giả chỉ nằm chờ. Chạy đúng file .exe đã publish với `--unattended`, rồi kiểm
tra: không cửa sổ nào mở ra, mã thoát đúng, file báo cáo tồn tại và đọc được. Đây là chỗ duy
nhất chứng minh được `AttachConsole` và `ShutdownMode` hoạt động thật.

**Không được làm hỏng đường cũ:** chạy không tham số vẫn phải mở giao diện bình thường.

## 14. Tài liệu kèm theo

- `README.md`: thêm mục "Chế độ không giám sát" với bảng tham số, bảng mã thoát, và một
  script PowerShell mẫu cài cho nhiều máy.
- File `.docx` hướng dẫn IT Helpdesk: thêm một chương, nhấn mạnh chuyện phải mở cửa sổ lệnh
  bằng quyền Administrator trước.
- `--help` phải tự nó đủ dùng, không bắt người ta đi tìm tài liệu.

## 15. Việc này không giải quyết những gì

Nói thẳng để không ai kỳ vọng nhầm:

- Không làm việc cài nhanh hơn. WinGet vẫn tải và cài với tốc độ như cũ. Thứ tiết kiệm được
  là **thời gian của người**, không phải thời gian của máy.
- Không cài được phần mềm không có trên WinGet.
- Không thay được việc chuẩn bị danh sách. Vẫn phải có ai đó dựng cấu hình trong giao diện trước.
