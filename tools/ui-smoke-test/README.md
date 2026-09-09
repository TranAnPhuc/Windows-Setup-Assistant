# Kiểm thử giao diện đầu-cuối (an toàn, không cài phần mềm thật)

Kịch bản này mở **đúng file .exe đã publish**, điều khiển giao diện thật bằng Windows UI Automation
và kiểm tra luồng đóng ứng dụng khi đang cài đặt.

`winget.exe` thật được thay bằng một bản **giả** (thư mục `fake-winget/`) chỉ nằm chờ,
nên chạy kịch bản này **không tải và không cài bất kỳ phần mềm nào** lên máy.

## Cách chạy

```powershell
# 1. Dựng winget giả
dotnet build fake-winget/winget.csproj -c Release

# 2. Publish ứng dụng (nếu chưa có)
dotnet publish ../../src/WindowsSetupAssistant.App -c Release -r win-x64 -o ../../publish

# 3. Tạo danh sách thử nghiệm tại ../../publish/Data/software-list.json
#    với 2 gói bất kỳ, ví dụ Fake.PackageA và Fake.PackageB

# 4. Chạy kịch bản
powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-UiSmokeTest.ps1
```

## Các bước được kiểm tra (16 mục)

1. Đóng ứng dụng khi **không** cài đặt → đóng ngay, không hỏi gì.
2. Bấm "Bắt đầu cài đặt" → hộp xác nhận trước khi cài hàng loạt phải hiện.
3. Bấm X lúc đang cài → hộp xác nhận hiện, trả lời **No** → ứng dụng vẫn chạy, hàng đợi không bị huỷ.
4. Bấm X lần nữa, trả lời **Yes** → ứng dụng thoát và tiến trình winget con bị dừng theo.

## Lưu ý về ngôn ngữ

Kịch bản tìm nút theo nhãn tiếng Việt (`"Bắt đầu cài đặt"`, `"Huỷ cài đặt"`...) nên kịch bản
tự động ghim `"language": "vi"` vào `Data/app-settings.json` trước khi chạy,
bảo đảm kịch bản chạy nhất quán trên mọi máy bất kể ngôn ngữ Windows của hệ thống.

Kết thúc phải thấy `Dat: 16 | Truot: 0`.

## Kịch bản thứ hai: chế độ không giám sát (`Run-UnattendedSmokeTest.ps1`)

`AttachConsole`, `ShutdownMode = OnExplicitShutdown` và mã thoát trả về hệ điều hành không thể
kiểm chứng trong test runner (`dotnet test`) - chúng chỉ lộ ra khi chạy đúng file `.exe` đã publish
từ một tiến trình cha có console thật. Kịch bản `Run-UnattendedSmokeTest.ps1` gọi thẳng `.exe` với
các tham số `--unattended`, `--help`, `--profile`, `--report` và kiểm tra: nội dung in ra console
cha, mã thoát, có mở cửa sổ nào hay không, và nội dung file báo cáo JSON.

Kịch bản dùng lại đúng `winget.exe` **giả** ở `fake-winget/` (đặt lên đầu `PATH`) như kịch bản UI
ở trên, nên cũng không cài phần mềm thật nào. Trước khi đụng vào `PATH`, kịch bản kiểm tra
`winget.exe` GIA thật sự tồn tại trong thư mục chỉ định (`-FakeWingetDir`) - `Resolve-Path` chỉ
xác nhận thư mục có thật, không xác nhận file bên trong, nên nếu thiếu file này (ví dụ bước build
winget giả bị bỏ sót) kịch bản **dừng ngay lập tức** với thông báo rõ ràng, không để rơi xuống
winget thật trên máy.

Trước khi chạy, kịch bản sao lưu thư mục `Data` cạnh file `.exe` (nếu có) và ghi đè bằng một danh
sách thử nghiệm riêng; khối `finally` luôn khôi phục lại `Data` gốc kể cả khi kịch bản lỗi giữa
chừng. Nếu ban đầu **chưa có** `Data` (bản publish mới), `finally` sẽ **xoá hẳn** thư mục thử
nghiệm đã tạo ra thay vì để nó nằm lại cạnh file `.exe`. Việc khôi phục/dọn dẹp được bọc trong
`try/catch` riêng để nếu nó tự thất bại (ví dụ file bị khoá) thì vẫn báo lỗi rõ ràng và bước dọn
thư mục tạm luôn được chạy, thay vì âm thầm bỏ dở.

Mục kiểm tra "không cửa sổ nào mở ra" chạy tiến trình **không** `-Wait`, dùng UI Automation đếm số
cửa sổ cấp cao theo ProcessId trong lúc tiến trình đang sống (cùng kỹ thuật với
`Run-UiSmokeTest.ps1`), rồi mới đợi tiến trình thoát hẳn - nhờ vậy mục này thật sự phát hiện được
nếu ứng dụng lỡ mở cửa sổ, chứ không phải lúc nào cũng đạt.

Winget giả mặc định "nằm chờ" 5 phút cho mỗi lệnh cài (`install`/`upgrade`) để phục vụ kịch bản UI
ở trên, nhưng thời gian này chỉnh được qua biến môi trường `WSA_FAKE_WINGET_DELAY_MS` (mili-giây).
Không đặt biến thì giữ nguyên hành vi cũ (5 phút) - `Run-UiSmokeTest.ps1` không bị ảnh hưởng.
`Run-UnattendedSmokeTest.ps1` đặt biến này rất ngắn (300ms) vì kịch bản không giám sát không cần
tiến trình cài sống lâu, nhờ đó cả 5 bước chỉ mất khoảng **vài giây đến vài chục giây**, thay vì
18-20 phút như trước.

Kết thúc phải thấy `Ket qua: 18 dat, 0 truot`.
