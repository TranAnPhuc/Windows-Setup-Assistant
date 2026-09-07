using WindowsSetupAssistant.Domain.Localization;
using System.Text.Json;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Infrastructure.Persistence;
using WindowsSetupAssistant.Tests.Fakes;

namespace WindowsSetupAssistant.Tests.Persistence;

/// <summary>
/// Test cho việc đọc/ghi JSON. Mỗi test dùng một thư mục tạm riêng rồi tự dọn dẹp,
/// nên không đụng tới dữ liệu thật của ứng dụng.
/// </summary>
public class JsonProfileRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _dataFile;
    private readonly RecordingLogger _logger = new();

    public JsonProfileRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "wsa-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _dataFile = Path.Combine(_tempDirectory, "Data", "software-list.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Không xoá được thư mục tạm thì bỏ qua, không làm hỏng test.
        }

        GC.SuppressFinalize(this);
    }

    private JsonProfileRepository CreateRepository() => new(_dataFile, _logger);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAsync_JsonWithNullEntries_PreservesValidData(bool import)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);
        const string json = """
        {
          "profiles": [
            null,
            { "name": "Máy thử nghiệm", "packages": [null, { "packageId": "Git.Git" }] },
            { "name": "Cấu hình rỗng", "packages": null }
          ]
        }
        """;
        await File.WriteAllTextAsync(_dataFile, json);
        var repository = CreateRepository();

        var catalog = import
            ? await repository.ImportAsync(_dataFile)
            : await repository.LoadAsync();

        Assert.Equal(2, catalog.Profiles.Count);
        Assert.Equal("Máy thử nghiệm", catalog.Profiles[0].Name);
        Assert.Equal("Git.Git", Assert.Single(catalog.Profiles[0].Packages).PackageId);
        Assert.Empty(catalog.Profiles[1].Packages);
        Assert.Equal(catalog.Profiles[0].Id, catalog.ActiveProfileId);
        Assert.Equal(json, await File.ReadAllTextAsync(_dataFile));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_dataFile)!, "*.bak"));
    }

    [Fact]
    public async Task LoadAsync_FirstRun_CreatesSeedFileWithVerifiedPackageIds()
    {
        var repository = CreateRepository();

        var catalog = await repository.LoadAsync();

        Assert.True(File.Exists(_dataFile));
        Assert.Equal(3, catalog.Profiles.Count);

        var allIds = catalog.Profiles.SelectMany(p => p.Packages).Select(p => p.PackageId).ToHashSet();

        Assert.Contains("Google.Chrome", allIds);
        Assert.Contains("Mozilla.Firefox", allIds);
        Assert.Contains("Microsoft.VisualStudioCode", allIds);
        Assert.Contains("Git.Git", allIds);
        Assert.Contains("OpenJS.NodeJS", allIds);
        Assert.Contains("7zip.7zip", allIds);
        Assert.Contains("VideoLAN.VLC", allIds);
        Assert.Contains("Notepad++.Notepad++", allIds);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryField()
    {
        var repository = CreateRepository();

        var package = new SoftwarePackage
        {
            Name = "Trình duyệt Cốc Cốc",
            PackageId = "CocCoc.CocCoc",
            Category = SoftwareCategory.Browser,
            Source = "winget",
            IsSelected = false,
            SortOrder = 0,
            Version = "1.2.3",
            Notes = "Ghi chú có dấu tiếng Việt"
        };

        var catalog = new SoftwareCatalog
        {
            Profiles = new List<InstallationProfile>
            {
                new() { Name = "Máy cá nhân", Packages = new List<SoftwarePackage> { package } }
            }
        };
        catalog.ActiveProfileId = catalog.Profiles[0].Id;

        await repository.SaveAsync(catalog);
        var loaded = await CreateRepository().LoadAsync();

        var loadedPackage = Assert.Single(loaded.Profiles[0].Packages);

        Assert.Equal("Trình duyệt Cốc Cốc", loadedPackage.Name);
        Assert.Equal("CocCoc.CocCoc", loadedPackage.PackageId);
        Assert.Equal(SoftwareCategory.Browser, loadedPackage.Category);
        Assert.False(loadedPackage.IsSelected);
        Assert.Equal("1.2.3", loadedPackage.Version);
        Assert.Equal("Ghi chú có dấu tiếng Việt", loadedPackage.Notes);
        Assert.Equal(catalog.Profiles[0].Id, loaded.ActiveProfileId);
    }

    [Fact]
    public async Task SaveAsync_WritesReadableJsonWithEnumNamesAndVietnameseText()
    {
        var repository = CreateRepository();
        var catalog = DefaultCatalogFactory.Create();

        await repository.SaveAsync(catalog);
        var json = await File.ReadAllTextAsync(_dataFile);

        Assert.Contains("\"category\": \"Browser\"", json);
        Assert.Contains("Máy cá nhân", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTemporaryFileBehind()
    {
        var repository = CreateRepository();

        await repository.SaveAsync(DefaultCatalogFactory.Create());

        Assert.False(File.Exists(_dataFile + ".tmp"));
    }

    [Fact]
    public async Task LoadAsync_CorruptedJson_PreservesOriginalFileAndThrowsWithoutOverwriting()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);
        const string corruptedJson = "{ đây không phải JSON hợp lệ ]]]";
        await File.WriteAllTextAsync(_dataFile, corruptedJson);

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => CreateRepository().LoadAsync());

        Assert.Equal(corruptedJson, await File.ReadAllTextAsync(_dataFile));
        Assert.Contains(_logger.Errors, e => e.Message.Key.Contains("không đọc được") || e.Message.Key == MessageKeys.CatalogReadFailed);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Key.Contains("danh sách mẫu") || e.Message.Key == MessageKeys.SeedCatalogCreated);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_dataFile)!, "*.bak"));
        Assert.False(File.Exists(_dataFile + ".tmp"));
    }

    [Fact]
    public async Task LoadAsync_ReadFailure_PreservesFileAndThrowsUntilReadable()
    {
        var repository = CreateRepository();
        var original = DefaultCatalogFactory.Create();
        original.Profiles[0].Name = "Dữ liệu cần giữ nguyên";
        await repository.SaveAsync(original);
        var originalBytes = await File.ReadAllBytesAsync(_dataFile);

        using (var lockedFile = new FileStream(_dataFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => repository.LoadAsync());
        }

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(_dataFile));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_dataFile)!, "*.bak"));
        Assert.False(File.Exists(_dataFile + ".tmp"));
        Assert.Contains(_logger.Errors, e => e.Message.Key.Contains("Không tải được dữ liệu") || e.Message.Key == MessageKeys.CatalogReadFailed);

        var recovered = await repository.LoadAsync();
        Assert.Equal("Dữ liệu cần giữ nguyên", recovered.Profiles[0].Name);
    }

    [Fact]
    public async Task LoadAsync_CorruptedJson_RepeatedLoadsPreserveOriginalFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);
        const string corrupted = "{ JSON lỗi nhưng phải giữ nguyên }";
        await File.WriteAllTextAsync(_dataFile, corrupted);
        var repository = CreateRepository();

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => repository.LoadAsync());
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => repository.LoadAsync());

        Assert.Equal(corrupted, await File.ReadAllTextAsync(_dataFile));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_dataFile)!, "*.bak"));
        Assert.False(File.Exists(_dataFile + ".tmp"));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Key.Contains("danh sách mẫu") || e.Message.Key == MessageKeys.SeedCatalogCreated);
    }

    [Fact]
    public async Task LoadAsync_DataPathIsDirectory_ReportsFailureWithoutWritingReplacement()
    {
        Directory.CreateDirectory(_dataFile);

        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => CreateRepository().LoadAsync());

        Assert.True(Directory.Exists(_dataFile));
        Assert.False(File.Exists(_dataFile + ".tmp"));
        Assert.Contains(_logger.Errors, e => e.Message.Key.Contains("Không tải được dữ liệu") || e.Message.Key == MessageKeys.CatalogReadFailed);
    }

    [Fact]
    public async Task LoadAsync_DropsPackagesWithInvalidPackageId()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);

        var json = """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "name": "Nhập từ máy khác",
              "packages": [
                { "name": "Chrome", "packageId": "Google.Chrome", "category": "Browser" },
                { "name": "Độc hại", "packageId": "x && shutdown /s", "category": "Other" }
              ]
            }
          ],
          "activeProfileId": "11111111-1111-1111-1111-111111111111"
        }
        """;

        await File.WriteAllTextAsync(_dataFile, json);

        var catalog = await CreateRepository().LoadAsync();

        var package = Assert.Single(catalog.Profiles[0].Packages);
        Assert.Equal("Google.Chrome", package.PackageId);
        Assert.Contains(_logger.Warnings, w => w.Message.Key.Contains("không hợp lệ") || w.Message.Key == MessageKeys.PackageDroppedInvalidId);
    }

    [Fact]
    public async Task ExportThenImport_PreservesData()
    {
        var repository = CreateRepository();
        var catalog = DefaultCatalogFactory.Create();
        var exportPath = Path.Combine(_tempDirectory, "backup", "danh-sach.json");

        await repository.ExportAsync(catalog, exportPath);
        var imported = await repository.ImportAsync(exportPath);

        Assert.True(File.Exists(exportPath));
        Assert.Equal(catalog.Profiles.Count, imported.Profiles.Count);
        Assert.Equal(
            catalog.Profiles.Sum(p => p.Packages.Count),
            imported.Profiles.Sum(p => p.Packages.Count));
    }

    [Fact]
    public async Task ImportAsync_MissingFile_ThrowsFileNotFound()
    {
        var repository = CreateRepository();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => repository.ImportAsync(Path.Combine(_tempDirectory, "khong-ton-tai.json")));
    }

    [Fact]
    public async Task ImportAsync_InvalidJson_ThrowsInvalidData()
    {
        var path = Path.Combine(_tempDirectory, "hong.json");
        await File.WriteAllTextAsync(path, "{{{{");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateRepository().ImportAsync(path));
    }

    [Fact]
    public async Task ImportAsync_EmptyProfileList_ThrowsInvalidData()
    {
        var path = Path.Combine(_tempDirectory, "rong.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new SoftwareCatalog(), CatalogJson.Options));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateRepository().ImportAsync(path));
    }

    [Fact]
    public void DataFilePath_DefaultsToDataFolderNextToApplication()
    {
        var repository = new JsonProfileRepository();

        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "Data", "software-list.json"),
            repository.DataFilePath);
    }
}
