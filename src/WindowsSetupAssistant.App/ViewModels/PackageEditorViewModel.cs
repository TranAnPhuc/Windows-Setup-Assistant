using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Validation;

namespace WindowsSetupAssistant.App.ViewModels;

/// <summary>ViewModel cho hộp thoại Thêm/Sửa phần mềm.</summary>
public sealed class PackageEditorViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _packageId = string.Empty;
    private CategoryOption _category = CategoryNames.All[0];
    private string _notes = string.Empty;
    private string _errorMessage = string.Empty;

    public PackageEditorViewModel(SoftwarePackage? existing = null)
    {
        IsEditing = existing is not null;

        if (existing is not null)
        {
            _name = existing.Name;
            _packageId = existing.PackageId;
            _notes = existing.Notes ?? string.Empty;
            _category = CategoryNames.All.FirstOrDefault(c => c.Value == existing.Category) ?? CategoryNames.All[0];
        }
    }

    public bool IsEditing { get; }

    public string Title => IsEditing ? "Sửa phần mềm" : "Thêm phần mềm";

    public IReadOnlyList<CategoryOption> Categories => CategoryNames.All;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string PackageId
    {
        get => _packageId;
        set => SetProperty(ref _packageId, value);
    }

    public CategoryOption Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Kiểm tra dữ liệu nhập. Trả về false và hiển thị lỗi nếu chưa hợp lệ.</summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Vui lòng nhập tên phần mềm.";
            return false;
        }

        if (!PackageIdValidator.TryValidate(PackageId, out var error))
        {
            ErrorMessage = error;
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    /// <summary>Ghi dữ liệu đã nhập vào entity.</summary>
    public void ApplyTo(SoftwarePackage package)
    {
        package.Name = Name.Trim();
        package.PackageId = PackageId.Trim();
        package.Category = Category.Value;
        package.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();

        if (string.IsNullOrWhiteSpace(package.Source))
        {
            package.Source = "winget";
        }
    }

    public SoftwarePackage CreatePackage(int sortOrder)
    {
        var package = new SoftwarePackage
        {
            SortOrder = sortOrder,
            IsSelected = true,
            Source = "winget",
            Category = SoftwareCategory.Other
        };

        ApplyTo(package);
        return package;
    }
}
