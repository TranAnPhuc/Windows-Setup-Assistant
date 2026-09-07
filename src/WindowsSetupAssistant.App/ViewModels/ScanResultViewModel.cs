using WindowsSetupAssistant.App.Localization;
using WindowsSetupAssistant.Application.Abstractions;
using WindowsSetupAssistant.Domain.Localization;
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
    private readonly IStringLocalizer _localizer;
    private bool _showSystemComponents;

    public ScanResultViewModel(MachineSnapshot snapshot, Action? onSaveRequested, IStringLocalizer localizer)
    {
        Snapshot = snapshot;
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        AutomaticEntries = new(snapshot.Entries.Where(e => e.Kind == InstalledSoftwareKind.WingetPackage).Select(e => new ScanEntryViewModel(e, true)));
        ManualEntries = new(snapshot.Entries.Where(e => e.Kind == InstalledSoftwareKind.ManualOnly).Select(e => new ScanEntryViewModel(e, true)));
        SystemEntries = new(snapshot.Entries.Where(e => e.Kind == InstalledSoftwareKind.SystemComponent).Select(e => new ScanEntryViewModel(e, false)));
    }

    public ScanResultViewModel(MachineSnapshot snapshot, IStringLocalizer localizer)
        : this(snapshot, null, localizer)
    {
    }

    public ScanResultViewModel(MachineSnapshot snapshot)
        : this(snapshot, null, LocalizationSource.Instance.Localizer)
    {
    }
    public MachineSnapshot Snapshot { get; }
    public ObservableCollection<ScanEntryViewModel> AutomaticEntries { get; }
    public ObservableCollection<ScanEntryViewModel> ManualEntries { get; }
    public ObservableCollection<ScanEntryViewModel> SystemEntries { get; }
    public bool ShowSystemComponents { get => _showSystemComponents; set => SetProperty(ref _showSystemComponents, value); }
    public string Headline => _localizer.Format(LocalizedText.Of(UiKeys.ScanResultHeadline, Snapshot.Entries.Count, Snapshot.MachineName));
    public string AutomaticHeader => _localizer.Format(LocalizedText.Of(UiKeys.ScanResultAutomaticHeader, AutomaticEntries.Count));
    public string ManualHeader => _localizer.Format(LocalizedText.Of(UiKeys.ScanResultManualHeader, ManualEntries.Count));
    public string SystemComponentLabel => _localizer.Format(LocalizedText.Of(UiKeys.ScanResultSystemLabel, SystemEntries.Count));
    public bool HasAnySelected => AutomaticEntries.Any(e => e.IsSelected) || ManualEntries.Any(e => e.IsSelected);
    public InstallationProfile BuildProfile(IEnumerable<string> existingNames)
    {
        var used = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var baseName = _localizer.Format(LocalizedText.Of(UiKeys.ScanResultProfileBaseName, Snapshot.MachineName));
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
