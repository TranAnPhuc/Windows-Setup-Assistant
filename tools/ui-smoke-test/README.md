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

Kết thúc phải thấy `Dat: 16 | Truot: 0`.
