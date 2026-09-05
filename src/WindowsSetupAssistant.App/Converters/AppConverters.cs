using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.Converters;

/// <summary>
/// Đổi tên khoá brush (chuỗi) thành Brush thật lấy từ theme đang dùng.
/// Nhờ cách này ViewModel chỉ cần trả về "SuccessBrush" mà không phải tham chiếu WPF.
/// </summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && WpfApplication.Current?.TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Đảo giá trị bool (dùng cho IsEnabled khi đang cài đặt).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : DependencyProperty.UnsetValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : DependencyProperty.UnsetValue;
}

/// <summary>bool -&gt; Visibility (true = Visible).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>bool -&gt; Visibility đảo ngược (true = Collapsed).</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Chuỗi rỗng thì ẩn, có nội dung thì hiện.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Tô màu dòng nhật ký theo mức độ.</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            LogLevel.Error => "DangerBrush",
            LogLevel.Warning => "WarningBrush",
            LogLevel.Debug => "MutedTextBrush",
            _ => "TextBrush"
        };

        return WpfApplication.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Hiển thị exit code dạng thập phân kèm hex cho dễ tra cứu.</summary>
public sealed class ExitCodeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int exitCode ? $"{exitCode} (0x{exitCode:X8})" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Đổi InstallOutcome thành chữ tiếng Việt cho bảng kết quả.</summary>
public sealed class InstallOutcomeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallOutcome outcome
            ? outcome switch
            {
                InstallOutcome.Succeeded => "Thành công",
                InstallOutcome.Upgraded => "Đã nâng cấp",
                InstallOutcome.Skipped => "Bỏ qua",
                InstallOutcome.AlreadyInstalled => "Đã có sẵn",
                InstallOutcome.Failed => "Thất bại",
                InstallOutcome.Cancelled => "Đã huỷ",
                _ => outcome.ToString()
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Tô màu ô Kết quả theo InstallOutcome.</summary>
public sealed class InstallOutcomeToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            InstallOutcome.Failed => "DangerBrush",
            InstallOutcome.Cancelled => "WarningBrush",
            InstallOutcome.Skipped or InstallOutcome.AlreadyInstalled => "MutedTextBrush",
            _ => "SuccessBrush"
        };

        return WpfApplication.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Hiển thị khoảng thời gian ngắn gọn: 12,3 giây.</summary>
public sealed class DurationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TimeSpan duration
            ? duration.TotalMinutes >= 1
                ? $"{duration.TotalMinutes:F1} phút"
                : $"{duration.TotalSeconds:F1} giây"
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
