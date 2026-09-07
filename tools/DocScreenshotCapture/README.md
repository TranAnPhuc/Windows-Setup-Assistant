# DocScreenshotCapture - Công cụ chụp màn hình tự động cho tài liệu

Công cụ dựng giao diện và kết xuất ảnh màn hình độ phân giải cao của Windows Setup Assistant trực tiếp ra thư mục `docs/images/`.

## Cách chạy

```powershell
dotnet run --project tools/DocScreenshotCapture/DocScreenshotCapture.csproj
```

Sau đó chạy lại script tạo tài liệu Word:
```powershell
cd docs
node build-guide.js
```
