using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.Domain.Enums;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>
/// ViewModel cho hộp xác nhận trước khi cài hàng loạt (yêu cầu bắt buộc của dự án).
/// Người dùng nhìn thấy chính xác những gì sắp được cài và chọn cách xử lý gói đã tồn tại.
/// </summary>
public sealed class InstallConfirmViewModel : ObservableObject
{
    private bool _upgradeExisting;

    public InstallConfirmViewModel(IReadOnlyList<SoftwarePackageViewModel> packages, ExistingPackageAction defaultAction)
    {
        Packages = packages;
        _upgradeExisting = defaultAction == ExistingPackageAction.Upgrade;
    }

    public IReadOnlyList<SoftwarePackageViewModel> Packages { get; }

    public int AlreadyInstalledCount => Packages.Count(p => p.InstallState == InstallState.Installed);

    public string Headline => $"Sắp cài {Packages.Count} phần mềm";

    public string Detail => AlreadyInstalledCount > 0
        ? $"Trong đó có {AlreadyInstalledCount} phần mềm đã có sẵn trên máy."
        : "Các phần mềm sẽ được cài lần lượt, không song song.";

    /// <summary>true = nâng cấp gói đã tồn tại, false = bỏ qua.</summary>
    public bool UpgradeExisting
    {
        get => _upgradeExisting;
        set
        {
            if (SetProperty(ref _upgradeExisting, value))
            {
                OnPropertyChanged(nameof(SkipExisting));
            }
        }
    }

    public bool SkipExisting
    {
        get => !_upgradeExisting;
        set => UpgradeExisting = !value;
    }

    public ExistingPackageAction SelectedAction =>
        UpgradeExisting ? ExistingPackageAction.Upgrade : ExistingPackageAction.Skip;
}
