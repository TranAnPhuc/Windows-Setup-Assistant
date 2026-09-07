namespace WindowsSetupAssistant.Domain.Entities;

using WindowsSetupAssistant.Domain.Models;

/// <summary>
/// Một cấu hình cài đặt, ví dụ "Máy cá nhân", "Máy lập trình", "Máy công ty".
/// Mỗi profile có danh sách phần mềm riêng.
/// </summary>
public sealed class InstallationProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<SoftwarePackage> Packages { get; set; } = new();

    public List<InstalledSoftwareEntry> ManualSoftware { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public InstallationProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Packages = Packages.Select(p => p.Clone()).ToList(),
        ManualSoftware = ManualSoftware.ToList()
    };

    public override string ToString() => Name;
}
