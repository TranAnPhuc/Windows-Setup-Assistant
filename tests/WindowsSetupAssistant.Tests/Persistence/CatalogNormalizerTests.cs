using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Infrastructure.Persistence;

namespace WindowsSetupAssistant.Tests.Persistence;

public class CatalogNormalizerTests
{
    [Fact]
    public void Normalize_NullEntries_PreservesValidProfilesAndPackages()
    {
        var package = new SoftwarePackage { Name = "Git", PackageId = "Git.Git" };
        var profile = new InstallationProfile
        {
            Name = "Máy lập trình",
            Packages = new List<SoftwarePackage> { null!, package, null! }
        };
        var catalog = new SoftwareCatalog
        {
            ActiveProfileId = profile.Id,
            Profiles = new List<InstallationProfile> { null!, profile, null! }
        };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);

        Assert.Same(profile, Assert.Single(normalized.Profiles));
        Assert.Same(package, Assert.Single(profile.Packages));
        Assert.Equal(profile.Id, normalized.ActiveProfileId);
        Assert.Equal("Máy lập trình", profile.Name);
        Assert.Equal(0, package.SortOrder);
    }

    [Fact]
    public void Normalize_OnlyNullProfiles_ReturnsUsableCatalog()
    {
        var catalog = new SoftwareCatalog
        {
            ActiveProfileId = Guid.NewGuid(),
            Profiles = new List<InstallationProfile> { null!, null! }
        };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);

        var profile = Assert.Single(normalized.Profiles);
        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal(profile.Id, normalized.ActiveProfileId);
        Assert.Empty(profile.Packages);
    }

    [Fact]
    public void Normalize_NullCatalog_ReturnsUsableCatalog()
    {
        var catalog = CatalogNormalizer.Normalize(null, out var removed);

        Assert.NotEmpty(catalog.Profiles);
        Assert.NotNull(catalog.ActiveProfileId);
        Assert.Empty(removed);
    }

    [Fact]
    public void Normalize_ActiveProfileIdPointingNowhere_FallsBackToFirstProfile()
    {
        var catalog = new SoftwareCatalog
        {
            ActiveProfileId = Guid.NewGuid(),
            Profiles = new List<InstallationProfile> { new() { Name = "A" } }
        };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);

        Assert.Equal(normalized.Profiles[0].Id, normalized.ActiveProfileId);
    }

    [Fact]
    public void Normalize_DuplicatePackageIdsInSameProfile_KeepsFirstOnly()
    {
        var catalog = new SoftwareCatalog
        {
            Profiles = new List<InstallationProfile>
            {
                new()
                {
                    Name = "A",
                    Packages = new List<SoftwarePackage>
                    {
                        new() { Name = "Chrome", PackageId = "Google.Chrome" },
                        new() { Name = "Chrome lần 2", PackageId = "google.chrome" }
                    }
                }
            }
        };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);

        var package = Assert.Single(normalized.Profiles[0].Packages);
        Assert.Equal("Chrome", package.Name);
    }

    [Fact]
    public void Normalize_RenumbersSortOrderContiguously()
    {
        var catalog = new SoftwareCatalog
        {
            Profiles = new List<InstallationProfile>
            {
                new()
                {
                    Name = "A",
                    Packages = new List<SoftwarePackage>
                    {
                        new() { Name = "B", PackageId = "B.B", SortOrder = 90 },
                        new() { Name = "A", PackageId = "A.A", SortOrder = 5 },
                        new() { Name = "C", PackageId = "C.C", SortOrder = 90 }
                    }
                }
            }
        };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);
        var packages = normalized.Profiles[0].Packages;

        Assert.Equal(new[] { "A.A", "B.B", "C.C" }, packages.Select(p => p.PackageId));
        Assert.Equal(new[] { 0, 1, 2 }, packages.Select(p => p.SortOrder));
    }

    [Fact]
    public void Normalize_EmptyGuids_AreReplaced()
    {
        var catalog = new SoftwareCatalog
        {
            Profiles = new List<InstallationProfile>
            {
                new()
                {
                    Id = Guid.Empty,
                    Name = "A",
                    Packages = new List<SoftwarePackage>
                    {
                        new() { Id = Guid.Empty, Name = "Chrome", PackageId = "Google.Chrome" }
                    }
                }
            }
        };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);

        Assert.NotEqual(Guid.Empty, normalized.Profiles[0].Id);
        Assert.NotEqual(Guid.Empty, normalized.Profiles[0].Packages[0].Id);
    }

    [Fact]
    public void Normalize_MissingNameAndSource_GetsSensibleDefaults()
    {
        var catalog = new SoftwareCatalog
        {
            Profiles = new List<InstallationProfile>
            {
                new()
                {
                    Name = "  ",
                    Packages = new List<SoftwarePackage>
                    {
                        new() { Name = "", PackageId = "Git.Git", Source = "" }
                    }
                }
            }
        };

        var normalized = CatalogNormalizer.Normalize(catalog, out _);

        Assert.Equal("Cấu hình không tên", normalized.Profiles[0].Name);
        Assert.Equal("Git.Git", normalized.Profiles[0].Packages[0].Name);
        Assert.Equal("winget", normalized.Profiles[0].Packages[0].Source);
    }

    [Fact]
    public void Normalize_ReportsRemovedPackages()
    {
        var catalog = new SoftwareCatalog
        {
            Profiles = new List<InstallationProfile>
            {
                new()
                {
                    Name = "A",
                    Packages = new List<SoftwarePackage>
                    {
                        new() { Name = "Xấu", PackageId = "--uninstall" }
                    }
                }
            }
        };

        CatalogNormalizer.Normalize(catalog, out var removed);

        Assert.Single(removed);
        Assert.Contains("--uninstall", removed[0]);
    }
}
