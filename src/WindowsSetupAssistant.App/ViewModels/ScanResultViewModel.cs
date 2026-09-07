using System.Collections.ObjectModel;
using WindowsSetupAssistant.App.Mvvm;
using WindowsSetupAssistant.Domain.Entities;
using WindowsSetupAssistant.Domain.Enums;
using WindowsSetupAssistant.Domain.Models;

namespace WindowsSetupAssistant.App.ViewModels;

public sealed class ScanEntryViewModel : ObservableObject
{
    private bool _isSelected;
    public ScanEntryViewModel(InstalledSoftwareEntry entry, bool selected) { Entry = entry; _isSelected = selected; }
    public InstalledSoftwareEntry Entry { get; }
    public string Name => Entry.Name;
    public string RawId => Entry.RawId;
    public string Version => Entry.Version;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

public sealed class ScanResultViewModel : ObservableObject
{
    private bool _showSystemComponents;
    public ScanResultViewModel(MachineSnapshot snapshot)
    {
        Snapshot = snapshot;
        AutomaticEntries = new(snapshot.Entries.Where(e => e.Kind == InstalledSoftwareKind.WingetPackage).Select(e => new ScanEntryViewModel(e, true)));
        ManualEntries = new(snapshot.Entries.Where(e => e.Kind == InstalledSoftwareKind.ManualOnly).Select(e => new ScanEntryViewModel(e, true)));
        SystemEntries = new(snapshot.Entries.Where(e => e.Kind == InstalledSoftwareKind.SystemComponent).Select(e => new ScanEntryViewModel(e, false)));
    }
    public MachineSnapshot Snapshot { get; }
    public ObservableCollection<ScanEntryViewModel> AutomaticEntries { get; }
    public ObservableCollection<ScanEntryViewModel> ManualEntries { get; }
    public ObservableCollection<ScanEntryViewModel> SystemEntries { get; }
    public bool ShowSystemComponents { get => _showSystemComponents; set => SetProperty(ref _showSystemComponents, value); }
    public string Headline => $"Đã quét {Snapshot.Entries.Count} phần mềm trên {Snapshot.MachineName}";
    public string AutomaticHeader => $"Cài tự động ({AutomaticEntries.Count})";
    public string ManualHeader => $"Ghi chú cài tay ({ManualEntries.Count})";
    public string SystemComponentLabel => $"Hiện cả app hệ thống ({SystemEntries.Count})";
    public bool HasAnySelected => AutomaticEntries.Any(e => e.IsSelected) || ManualEntries.Any(e => e.IsSelected);
    public InstallationProfile BuildProfile(IEnumerable<string> existingNames)
    {
        var used = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var baseName = $"Sao lưu {Snapshot.MachineName}";
        var name = baseName; var i = 2;
        while (!used.Add(name)) name = $"{baseName} ({i++})";
        return new InstallationProfile
        {
            Name = name,
            Packages = AutomaticEntries.Where(e => e.IsSelected).Select((e, index) => new SoftwarePackage
            { Name = e.Name, PackageId = e.RawId, Source = e.Entry.Source ?? "winget", SortOrder = index }).ToList(),
            ManualSoftware = ManualEntries.Where(e => e.IsSelected).Select(e => e.Entry).ToList()
        };
    }
}
