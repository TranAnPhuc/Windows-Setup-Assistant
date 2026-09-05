using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>
/// Bọc <see cref="SoftwarePackage"/> để hiển thị trên lưới.
///
/// Vì sao không bind thẳng vào entity? Vì entity là dữ liệu lưu xuống JSON,
/// còn màn hình cần thêm trạng thái runtime: đã cài chưa, kết quả lần cài gần nhất,
/// màu sắc và chữ hiển thị. Tách ra giúp file JSON luôn sạch.
/// </summary>
public sealed class SoftwarePackageViewModel : ObservableObject
{
    private InstallState _installState = InstallState.Unknown;
    private InstallationResult? _lastResult;
    private string? _installedVersion;

    public SoftwarePackageViewModel(SoftwarePackage model, Action? onSelectionChanged = null)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        OnSelectionChanged = onSelectionChanged;
    }

    public SoftwarePackage Model { get; }

    private Action? OnSelectionChanged { get; }

    public Guid Id => Model.Id;

    public string Name => Model.Name;

    public string PackageId => Model.PackageId;

    public SoftwareCategory Category => Model.Category;

    public string CategoryDisplayName => CategoryNames.Display(Model.Category);

    public string? Notes => Model.Notes;

    public int SortOrder => Model.SortOrder;

    public bool IsSelected
    {
        get => Model.IsSelected;
        set
        {
            if (Model.IsSelected == value)
            {
                return;
            }

            Model.IsSelected = value;
            OnPropertyChanged();
            OnSelectionChanged?.Invoke();
        }
    }

    public InstallState InstallState
    {
        get => _installState;
        set
        {
            if (SetProperty(ref _installState, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrushKey));
            }
        }
    }

    public string? InstalledVersion
    {
        get => _installedVersion;
        set
        {
            if (SetProperty(ref _installedVersion, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public InstallationResult? LastResult
    {
        get => _lastResult;
        set
        {
            if (SetProperty(ref _lastResult, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrushKey));
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _lastResult?.Outcome == InstallOutcome.Failed;

    /// <summary>Chữ hiển thị ở cột Trạng thái.</summary>
    public string StatusText
    {
        get
        {
            if (_lastResult is not null)
            {
                return _lastResult.Outcome switch
                {
                    InstallOutcome.Succeeded => "Đã cài xong",
                    InstallOutcome.Upgraded => "Đã nâng cấp",
                    InstallOutcome.Skipped => "Bỏ qua (đã có)",
                    InstallOutcome.AlreadyInstalled => "Đã là bản mới nhất",
                    InstallOutcome.Failed => "Thất bại",
                    InstallOutcome.Cancelled => "Đã huỷ",
                    _ => "Không rõ"
                };
            }

            return InstallState switch
            {
                InstallState.Installed => string.IsNullOrWhiteSpace(InstalledVersion)
                    ? "Đã cài"
                    : $"Đã cài ({InstalledVersion})",
                InstallState.NotInstalled => "Chưa cài",
                InstallState.Checking => "Đang kiểm tra...",
                _ => "Chưa kiểm tra"
            };
        }
    }

    /// <summary>Khoá brush trong Theme để tô màu trạng thái (dùng cùng DynamicResource).</summary>
    public string StatusBrushKey
    {
        get
        {
            if (_lastResult is not null)
            {
                return _lastResult.Outcome switch
                {
                    InstallOutcome.Failed => "DangerBrush",
                    InstallOutcome.Cancelled => "WarningBrush",
                    InstallOutcome.Skipped or InstallOutcome.AlreadyInstalled => "MutedTextBrush",
                    _ => "SuccessBrush"
                };
            }

            return InstallState switch
            {
                InstallState.Installed => "SuccessBrush",
                InstallState.NotInstalled => "MutedTextBrush",
                _ => "MutedTextBrush"
            };
        }
    }

    /// <summary>Gọi sau khi sửa entity để giao diện vẽ lại toàn bộ các cột.</summary>
    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(PackageId));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(CategoryDisplayName));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(SortOrder));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrushKey));
    }
}

/// <summary>Tên hiển thị tiếng Việt cho từng nhóm phần mềm.</summary>
public static class CategoryNames
{
    public static string Display(SoftwareCategory category) => category switch
    {
        SoftwareCategory.Browser => "Trình duyệt",
        SoftwareCategory.Development => "Lập trình",
        SoftwareCategory.Office => "Văn phòng",
        SoftwareCategory.Entertainment => "Giải trí",
        SoftwareCategory.Utility => "Tiện ích",
        _ => "Khác"
    };

    public static IReadOnlyList<CategoryOption> All { get; } = Enum
        .GetValues<SoftwareCategory>()
        .Select(c => new CategoryOption(c, Display(c)))
        .ToList();
}

public sealed record CategoryOption(SoftwareCategory Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
