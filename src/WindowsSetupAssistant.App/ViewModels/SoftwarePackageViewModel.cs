using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Localization;
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

    private readonly IStringLocalizer _localizer;

    public SoftwarePackageViewModel(SoftwarePackage model, Action? onSelectionChanged, IStringLocalizer localizer)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        OnSelectionChanged = onSelectionChanged;
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    public SoftwarePackageViewModel(SoftwarePackage model, IStringLocalizer localizer)
        : this(model, null, localizer)
    {
    }

    public SoftwarePackageViewModel(SoftwarePackage model, Action? onSelectionChanged = null)
        : this(model, onSelectionChanged, LocalizationSource.Instance.Localizer)
    {
    }

    public SoftwarePackage Model { get; }

    private Action? OnSelectionChanged { get; }

    public Guid Id => Model.Id;

    public string Name => Model.Name;

    public string PackageId => Model.PackageId;

    public SoftwareCategory Category => Model.Category;

    public string CategoryDisplayName => CategoryNames.Display(Model.Category, _localizer);

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
                    InstallOutcome.Succeeded => _localizer[UiKeys.StatusOutcomeSucceeded],
                    InstallOutcome.Upgraded => _localizer[UiKeys.StatusOutcomeUpgraded],
                    InstallOutcome.Skipped => _localizer[UiKeys.StatusOutcomeSkipped],
                    InstallOutcome.AlreadyInstalled => _localizer[UiKeys.StatusOutcomeAlreadyInstalled],
                    InstallOutcome.Failed => _localizer[UiKeys.StatusOutcomeFailed],
                    InstallOutcome.Cancelled => _localizer[UiKeys.StatusOutcomeCancelled],
                    _ => _localizer[UiKeys.StatusOutcomeUnknown]
                };
            }

            return InstallState switch
            {
                InstallState.Installed => string.IsNullOrWhiteSpace(InstalledVersion)
                    ? _localizer[UiKeys.StatusStateInstalled]
                    : _localizer.Format(LocalizedText.Of(UiKeys.StatusStateInstalledVersion, InstalledVersion)),
                InstallState.NotInstalled => _localizer[UiKeys.StatusStateNotInstalled],
                InstallState.Checking => _localizer[UiKeys.StatusStateChecking],
                _ => _localizer[UiKeys.StatusStateNotChecked]
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
    public static string Display(SoftwareCategory category, IStringLocalizer localizer) => category switch
    {
        SoftwareCategory.Browser => localizer[UiKeys.CategoryBrowser],
        SoftwareCategory.Development => localizer[UiKeys.CategoryDevelopment],
        SoftwareCategory.Office => localizer[UiKeys.CategoryOffice],
        SoftwareCategory.Entertainment => localizer[UiKeys.CategoryEntertainment],
        SoftwareCategory.Utility => localizer[UiKeys.CategoryUtility],
        _ => localizer[UiKeys.CategoryOther]
    };

    public static string Display(SoftwareCategory category) =>
        Display(category, LocalizationSource.Instance.Localizer);

    public static IReadOnlyList<CategoryOption> GetAll(IStringLocalizer localizer) => Enum
        .GetValues<SoftwareCategory>()
        .Select(c => new CategoryOption(c, Display(c, localizer)))
        .ToList();

    public static IReadOnlyList<CategoryOption> All =>
        GetAll(LocalizationSource.Instance.Localizer);
}

public sealed record CategoryOption(SoftwareCategory Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
