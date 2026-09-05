namespace WindowsSetupAssistant.Domain.Entities;

/// <summary>
/// Node gốc của file Data/software-list.json.
/// Chứa toàn bộ profile và profile đang được chọn.
/// </summary>
public sealed class SoftwareCatalog
{
    /// <summary>Phiên bản schema, dùng để migrate về sau khi format thay đổi.</summary>
    public int SchemaVersion { get; set; } = 1;

    public Guid? ActiveProfileId { get; set; }

    public List<InstallationProfile> Profiles { get; set; } = new();

    /// <summary>Lấy profile đang chọn, tự động fallback về profile đầu tiên.</summary>
    public InstallationProfile? GetActiveProfile()
    {
        if (Profiles.Count == 0)
        {
            return null;
        }

        return Profiles.FirstOrDefault(p => p.Id == ActiveProfileId) ?? Profiles[0];
    }
}
