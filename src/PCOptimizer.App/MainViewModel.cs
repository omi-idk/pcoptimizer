using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal sealed record InfoCard(string Label, string Value, string Detail);

internal sealed class ExecutableTweakChoice(ExecutableTweak definition) : INotifyPropertyChanged
{
    private bool selected;
    private string state = "Sin analizar";
    private string values = "Pulsa Analizar para leer los valores actuales.";
    public ExecutableTweak Definition { get; } = definition;
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Category => Definition.Category;
    public string Benefit => Definition.Benefit;
    public string Risk => Definition.Risk;
    public string Activation => Definition.Activation;
    public string Source => Definition.Source;
    public string Scope => Definition.Targets.Any(t => t.Machine) ? "EQUIPO · REQUIERE ADMINISTRADOR" : "TU USUARIO · SIN ELEVACIÓN PARA ESCRIBIR";
    public string Targets => string.Join("\n", Definition.Targets.Select(t => $"{(t.Machine ? "HKLM" : "HKCU")}\\{t.Path}\\{t.Name} → {t.Desired.Display}"));
    public bool IsSelected { get => selected; set { selected = value; Changed(); } }
    public string State { get => state; set { state = value; Changed(); } }
    public string Values { get => values; set { values = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class SoftwareChoice(string id, string name, string detail, string website = "",
    string category = "", string sourceLabel = "", string notes = "") : INotifyPropertyChanged
{
    private bool isSelected;
    private bool canSelect = true;
    private string availability = "";
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Detail { get; } = detail;
    public string Website { get; } = website;
    public string Category { get; } = category;
    public string SourceLabel { get; } = sourceLabel;
    public string Notes { get; } = notes;
    public string Monogram => Name[..1].ToUpperInvariant();
    public string ReviewLabel => string.IsNullOrEmpty(Notes) ? "" : "Revisar condiciones";
    public bool IsSelected { get => isSelected; set { isSelected = value; Changed(); } }
    public bool CanSelect { get => canSelect; set { canSelect = value; Changed(); } }
    public string Availability { get => availability; set { availability = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class MainViewModel : INotifyPropertyChanged
{
    private string title = "Inicio";
    private string subtitle = "Conoce tu equipo antes de cambiar su configuración.";
    private string status = "Consultando información de Windows…";
    private bool isIdle;
    private PowerPlan? selectedPlan;
    private string searchText = "";
    private string selectedCategory = "Todas las categorías";
    private string tweakSearchText = "";
    private bool createRestorePointBeforeChanges = true;
    private RecoverySession? selectedRecoverySession;
    private string executableSearch = "";
    private string selectedProfile = "Personalizado · selecciona los ajustes";
    private TweakOperation? selectedTweakOperation;

    public MainViewModel()
    {
        InstallView = new ListCollectionView(InstallChoices);
        InstallView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SoftwareChoice.Category)));
        InstallView.Filter = value => value is SoftwareChoice choice
            && (SelectedCategory == "Todas las categorías" || choice.Category == SelectedCategory)
            && CatalogSearch.Matches(SearchText, choice.Name, choice.Id, choice.Category, choice.Detail);
        foreach (var choice in InstallChoices)
            choice.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SoftwareChoice.IsSelected)) Changed(nameof(SelectionSummary)); };
        TweakView = new ListCollectionView(TweakCatalog.Items.ToArray());
        TweakView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TweakDefinition.Category)));
        TweakView.Filter = value => value is TweakDefinition tweak
            && tweak.ExecutableId is null && CatalogSearch.Matches(TweakSearchText, tweak.Name, tweak.Id, tweak.Category, tweak.Risks);
        ExecutableView = new ListCollectionView(ExecutableChoices);
        ExecutableView.Filter = value => value is ExecutableTweakChoice choice
            && CatalogSearch.Matches(ExecutableSearch, choice.Name, choice.Category, choice.Benefit);
        foreach (var choice in ExecutableChoices)
            choice.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ExecutableTweakChoice.IsSelected)) Changed(nameof(TweakSelectionSummary)); };
    }

    public string Title { get => title; set { title = value; Changed(); } }
    public string Subtitle { get => subtitle; set { subtitle = value; Changed(); } }
    public string Status { get => status; set { status = value; Changed(); } }
    public bool IsIdle { get => isIdle; set { isIdle = value; Changed(); } }
    public PowerPlan? SelectedPlan { get => selectedPlan; set { selectedPlan = value; Changed(); } }
    public string SearchText { get => searchText; set { searchText = value; Changed(); RefreshCatalog(); } }
    public string SelectedCategory { get => selectedCategory; set { selectedCategory = value; Changed(); RefreshCatalog(); } }
    public ICollectionView InstallView { get; }
    public IReadOnlyList<string> Categories { get; } = new[] { "Todas las categorías" }
        .Concat(SoftwareCatalog.Packages.Select(p => p.Category).Distinct()).ToArray();
    public string CatalogSummary => $"{InstallView.Cast<object>().Count()} de {InstallChoices.Count} aplicaciones";
    public string SelectionSummary => $"{InstallChoices.Count(p => p.IsSelected)} seleccionadas en todo el catálogo";
    public string TweakSearchText { get => tweakSearchText; set { tweakSearchText = value; Changed(); TweakView.Refresh(); } }
    public ICollectionView TweakView { get; }
    public ObservableCollection<ExecutableTweakChoice> ExecutableChoices { get; } = new(ExecutableTweaks.Items.Select(t => new ExecutableTweakChoice(t)));
    public ICollectionView ExecutableView { get; }
    public string ExecutableSummary => $"{ExecutableChoices.Count} ajustes ejecutables · valores anteriores guardados";
    public string ExecutableSearch { get => executableSearch; set { executableSearch = value; Changed(); ExecutableView.Refresh(); } }
    public string SelectedProfile { get => selectedProfile; set { selectedProfile = value; Changed(); } }
    public string TweakSelectionSummary => $"{ExecutableChoices.Count(t => t.IsSelected)} ajustes seleccionados · se revisarán antes de aplicar";
    public ObservableCollection<TweakOperation> TweakOperations { get; } = [];
    public ObservableCollection<InstalledDriver> Drivers { get; } = [];
    public TweakOperation? SelectedTweakOperation { get => selectedTweakOperation; set { selectedTweakOperation = value; Changed(); Changed(nameof(SelectedTweakChanges)); } }
    public string SelectedTweakChanges => SelectedTweakOperation is not { } operation ? "Selecciona un registro para revisar los valores guardados." :
        string.Join("\n\n", operation.Changes.Select(c => $"{ExecutableTweaks.Get(c.TweakId).Name} · {c.TargetId}\nAnterior: {c.Before.Display}\nSolicitado: {c.After.Display}\n{c.State}"));
    public bool CreateRestorePointBeforeChanges { get => createRestorePointBeforeChanges; set { createRestorePointBeforeChanges = value; Changed(); } }
    public ObservableCollection<RecoverySession> RecoverySessions { get; } = [];
    public RecoverySession? SelectedRecoverySession
    {
        get => selectedRecoverySession;
        set { selectedRecoverySession = value; Changed(); Changed(nameof(RecoveryActions)); }
    }
    public IReadOnlyList<RecoveryAction> RecoveryActions => SelectedRecoverySession?.Actions ?? [];
    public ObservableCollection<InfoCard> Cards { get; } = [];
    public ObservableCollection<PowerPlan> PowerPlans { get; } = [];
    public ObservableCollection<SoftwareChoice> InstallChoices { get; } = new(SoftwareCatalog.Packages
        .Select(p => new SoftwareChoice(p.Id, p.Name, p.Description, p.Website, p.Category, p.SourceLabel, p.Notes)));
    public ObservableCollection<SoftwareChoice> OptionalAppChoices { get; } = new(SoftwareCatalog.OptionalApps
        .Select(p => new SoftwareChoice(p.Id, p.Name, p.Consequence) { CanSelect = false, Availability = "Sin analizar" }));
    public event PropertyChangedEventHandler? PropertyChanged;
    private void RefreshCatalog() { InstallView.Refresh(); Changed(nameof(CatalogSummary)); }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
