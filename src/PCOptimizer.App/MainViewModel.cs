using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using PCOptimizer.Core;

namespace PCOptimizer.App;

internal sealed record InfoCard(string Label, string Value, string Detail);
internal enum TweakReadState { Unknown, Ready, Configured, Unavailable }

internal sealed class ExecutableTweakChoice(ExecutableTweak definition) : INotifyPropertyChanged
{
    private bool selected;
    private TweakReadState readState;
    private string values = "Aún no se ha consultado el registro.";
    private string proposedValues = "Analiza el estado para calcular los cambios.";
    public ExecutableTweak Definition { get; } = definition;
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Category => Id == "mouse-acceleration" ? "Entrada" : Definition.Category switch
    { "Experiencia de Windows" => "Personalización", "Avanzado · equipo" => "Gráficos", "Windows Update · equipo" => "Windows Update", var value => value };
    public string Benefit => Definition.Benefit;
    public string Risk => Definition.Risk;
    public string Activation => Definition.Activation;
    public string Source => Definition.Source;
    public string Scope => Definition.Targets.Any(t => t.Machine) ? "Equipo · requiere administrador" : "Usuario actual";
    public string RiskLabel => Definition.RiskLevel switch { TweakRisk.Advanced => "Avanzado", TweakRisk.Standard => "Revisar", _ => "Preferencia" };
    public string RiskColor => Definition.RiskLevel == TweakRisk.Advanced ? "#EDB96F" : "#9AA8BC";
    public string Targets => string.Join("\n", Definition.Targets.Select(t => $"{(t.Machine ? "HKLM" : "HKCU")}\\{t.Path}\\{t.Name}"));
    public bool IsSelected { get => selected; set { if (selected == value) return; selected = value; Changed(); } }
    public TweakReadState ReadState => readState;
    public bool CanSelect => readState is TweakReadState.Ready or TweakReadState.Configured;
    public string State => readState switch { TweakReadState.Ready => "Listo", TweakReadState.Configured => "Configurado", TweakReadState.Unavailable => "No disponible", _ => "Sin analizar" };
    public string StateColor => readState switch { TweakReadState.Configured => "#6BD3AB", TweakReadState.Unavailable => "#EDB96F", TweakReadState.Ready => "#7DB3FF", _ => "#7B8799" };
    public string Values => values;
    public string ProposedValues => proposedValues;
    public void SetSnapshot(TweakOperation operation)
    {
        readState = operation.Changes.All(c => c.Before == c.After) ? TweakReadState.Configured : TweakReadState.Ready;
        values = string.Join("\n", operation.Changes.Select(c => $"{ExecutableTweaks.Target(c.TweakId, c.TargetId).Name}: {c.Before.Display}"));
        proposedValues = string.Join("\n", operation.Changes.Select(c => $"{ExecutableTweaks.Target(c.TweakId, c.TargetId).Name}: {c.After.Display}"));
        NotifyState();
    }
    public void SetUnavailable(string reason)
    {
        readState = TweakReadState.Unavailable;
        values = reason;
        proposedValues = "No se aplicará mientras no pueda comprobarse su compatibilidad y estado.";
        IsSelected = false;
        NotifyState();
    }
    private void NotifyState()
    {
        foreach (var property in new[] { nameof(ReadState), nameof(CanSelect), nameof(State), nameof(StateColor), nameof(Values), nameof(ProposedValues) }) Changed(property);
    }
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
    private string title = "Resumen";
    private string subtitle = "Hardware, configuración y actividad de tu equipo.";
    private string status = "Consultando Windows…";
    private bool isIdle;
    public bool UsesGameBarClips { get; set; } = true;
    private PowerPlan? selectedPlan;
    private string searchText = "";
    private string selectedCategory = "Todas las categorías";
    private string tweakSearchText = "";
    private bool createRestorePointBeforeChanges = true;
    private RecoverySession? selectedRecoverySession;
    private string executableSearch = "";
    private string executableCategory = "Todas las áreas";
    private string executableStatus = "Todos los estados";
    private string selectedProfile = "Personalizado";
    private string activeProfileId = "";
    private string profileNote = "Selecciona un perfil o elige ajustes individuales. Nada cambia hasta que confirmes.";
    private TweakOperation? selectedTweakOperation;
    private ExecutableTweakChoice? selectedExecutableTweak;
    private string tweakBackupIssue = "";
    private bool changingProfile;

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
        TweakView.Filter = value => value is TweakDefinition tweak && tweak.ExecutableId is null
            && CatalogSearch.Matches(TweakSearchText, tweak.Name, tweak.Id, tweak.Category, tweak.Risks);
        ExecutableView = new ListCollectionView(ExecutableChoices);
        ExecutableView.Filter = value => value is ExecutableTweakChoice choice
            && (ExecutableCategory == "Todas las áreas" || choice.Category == ExecutableCategory)
            && (ExecutableStatus switch
            {
                "Seleccionados" => choice.IsSelected,
                "Listos" => choice.ReadState == TweakReadState.Ready,
                "Configurados" => choice.ReadState == TweakReadState.Configured,
                "No disponibles" => choice.ReadState == TweakReadState.Unavailable,
                _ => true
            })
            && CatalogSearch.Matches(ExecutableSearch, choice.Name, choice.Category, choice.Benefit);
        foreach (var choice in ExecutableChoices)
            choice.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ExecutableTweakChoice.IsSelected))
                {
                    if (choice.IsSelected && choice.Definition.ExclusiveGroup is { } group)
                        foreach (var other in ExecutableChoices.Where(t => t != choice && t.Definition.ExclusiveGroup == group)) other.IsSelected = false;
                    if (!changingProfile) { ActiveProfileId = ""; SelectedProfile = "Personalizado"; ProfileNote = "Selección manual. Revisa el alcance y los riesgos antes de aplicar."; }
                    Changed(nameof(TweakSelectionSummary)); Changed(nameof(SelectedTweakCount)); Changed(nameof(CanApplySelected));
                    if (!changingProfile && ExecutableStatus == "Seleccionados")
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Background, new Action(RefreshTweaks));
                }
                if (args.PropertyName == nameof(ExecutableTweakChoice.ReadState)) { Changed(nameof(ConfiguredSummary)); Changed(nameof(AvailableSummary)); }
            };
        SelectedExecutableTweak = ExecutableChoices.FirstOrDefault();
    }

    public string Title { get => title; set { title = value; Changed(); } }
    public string Subtitle { get => subtitle; set { subtitle = value; Changed(); } }
    public string Status { get => status; set { status = value; Changed(); } }
    public bool IsIdle { get => isIdle; set { isIdle = value; Changed(); Changed(nameof(CanApplySelected)); Changed(nameof(CanUndoSelected)); Changed(nameof(CanRestoreSavedPlan)); } }
    public PowerPlan? SelectedPlan { get => selectedPlan; set { selectedPlan = value; Changed(); } }
    public string SearchText { get => searchText; set { searchText = value; Changed(); RefreshCatalog(); } }
    public string SelectedCategory { get => selectedCategory; set { selectedCategory = value; Changed(); RefreshCatalog(); } }
    public ICollectionView InstallView { get; }
    public IReadOnlyList<string> Categories { get; } = new[] { "Todas las categorías" }.Concat(SoftwareCatalog.Packages.Select(p => p.Category).Distinct()).ToArray();
    public string CatalogSummary => $"{InstallView.Cast<object>().Count()} de {InstallChoices.Count} aplicaciones";
    public string SelectionSummary => $"{InstallChoices.Count(p => p.IsSelected)} seleccionadas";
    public string TweakSearchText { get => tweakSearchText; set { tweakSearchText = value; Changed(); TweakView.Refresh(); } }
    public ICollectionView TweakView { get; }
    public ObservableCollection<ExecutableTweakChoice> ExecutableChoices { get; } = new(ExecutableTweaks.Items.Select(t => new ExecutableTweakChoice(t)));
    public ICollectionView ExecutableView { get; }
    public IReadOnlyList<string> ExecutableCategories => new[] { "Todas las áreas" }.Concat(ExecutableChoices.Select(t => t.Category).Distinct()).ToArray();
    public IReadOnlyList<string> ExecutableStates { get; } = ["Todos los estados", "Listos", "Configurados", "Seleccionados", "No disponibles"];
    public string ExecutableSummary => $"{ExecutableChoices.Count} ajustes ejecutables";
    public string AvailableSummary => $"{ExecutableChoices.Count(t => t.CanSelect)} compatibles";
    public string ConfiguredSummary
    {
        get { var count = ExecutableChoices.Count(t => t.ReadState == TweakReadState.Configured); return $"{count} {(count == 1 ? "configurado" : "configurados")}"; }
    }
    public string VisibleTweakSummary => $"{ExecutableView.Cast<object>().Count()} resultados";
    public string TweakBackupIssue { get => tweakBackupIssue; set { tweakBackupIssue = value; Changed(); Changed(nameof(CanApplySelected)); } }
    public string ExecutableSearch { get => executableSearch; set { executableSearch = value; Changed(); RefreshTweaks(); } }
    public string ExecutableCategory { get => executableCategory; set { executableCategory = value; Changed(); RefreshTweaks(); } }
    public string ExecutableStatus { get => executableStatus; set { executableStatus = value; Changed(); RefreshTweaks(); } }
    public ExecutableTweakChoice? SelectedExecutableTweak { get => selectedExecutableTweak; set { selectedExecutableTweak = value; Changed(); } }
    public string SelectedProfile { get => selectedProfile; set { selectedProfile = value; Changed(); } }
    public string ActiveProfileId { get => activeProfileId; set { activeProfileId = value; Changed(); } }
    public string ProfileNote { get => profileNote; set { profileNote = value; Changed(); } }
    public string MinimalCount => $"{ExecutableTweaks.Profile("minimal").Count} ajustes";
    public string RecommendedCount => $"{ExecutableTweaks.Profile("recommended").Count} ajustes";
    public string GamingCount => $"{ExecutableTweaks.Profile("gaming").Count} ajustes";
    public string IntensiveCount => $"{ExecutableTweaks.Profile("intensive").Count} ajustes";
    public int SelectedTweakCount => ExecutableChoices.Count(t => t.IsSelected);
    public bool CanApplySelected => IsIdle && SelectedTweakCount > 0 && string.IsNullOrEmpty(TweakBackupIssue);
    public bool CanUndoSelected => IsIdle && SelectedTweakOperation is not null;
    public bool CanRestoreSavedPlan => IsIdle && SelectedRecoverySession?.Actions.Any(a => a.Kind == RecoveryActionKind.PowerPlan) == true;
    public string TweakSelectionSummary => $"{SelectedTweakCount} seleccionados en todo el catálogo";
    public ObservableCollection<TweakOperation> TweakOperations { get; } = [];
    public ObservableCollection<InstalledDriver> Drivers { get; } = [];
    public TweakOperation? SelectedTweakOperation { get => selectedTweakOperation; set { selectedTweakOperation = value; Changed(); Changed(nameof(SelectedTweakChanges)); Changed(nameof(CanUndoSelected)); } }
    public string SelectedTweakChanges => SelectedTweakOperation is not { } operation ? "Selecciona una operación para revisar los valores guardados." :
        string.Join("\n\n", operation.Changes.Select(c => $"{ExecutableTweaks.Get(c.TweakId).Name} · {c.TargetId}\nAnterior: {c.Before.Display}\nSolicitado: {c.After.Display}\n{c.State}"));
    public bool CreateRestorePointBeforeChanges { get => createRestorePointBeforeChanges; set { createRestorePointBeforeChanges = value; Changed(); } }
    public ObservableCollection<RecoverySession> RecoverySessions { get; } = [];
    public RecoverySession? SelectedRecoverySession { get => selectedRecoverySession; set { selectedRecoverySession = value; Changed(); Changed(nameof(RecoveryActions)); Changed(nameof(CanRestoreSavedPlan)); } }
    public IReadOnlyList<RecoveryAction> RecoveryActions => SelectedRecoverySession?.Actions ?? [];
    public ObservableCollection<InfoCard> Cards { get; } = [];
    public ObservableCollection<PowerPlan> PowerPlans { get; } = [];
    public ObservableCollection<SoftwareChoice> InstallChoices { get; } = new(SoftwareCatalog.Packages.Select(p => new SoftwareChoice(p.Id, p.Name, p.Description, p.Website, p.Category, p.SourceLabel, p.Notes)));
    public ObservableCollection<SoftwareChoice> OptionalAppChoices { get; } = new(SoftwareCatalog.OptionalApps.Select(p => new SoftwareChoice(p.Id, p.Name, p.Consequence) { CanSelect = false, Availability = "Sin analizar" }));

    public void SelectProfile(string id)
    {
        var ids = id == "clear" ? [] : ExecutableTweaks.Profile(id);
        changingProfile = true;
        try { foreach (var choice in ExecutableChoices) choice.IsSelected = ids.Contains(choice.Id) && choice.CanSelect; }
        finally { changingProfile = false; }
        ActiveProfileId = id;
        SelectedProfile = id switch { "minimal" => "Mínimo", "recommended" => "Recomendado", "gaming" => "Gaming", "intensive" => "Intensivo", _ => "Personalizado" };
        var skipped = ids.Count - SelectedTweakCount;
        ProfileNote = id == "clear" ? "Selección vacía. Nada se ha modificado." : $"{SelectedProfile}: {SelectedTweakCount} ajustes seleccionados." +
            (skipped == 0 ? " Revisa la selección antes de aplicar." : $" {skipped} no disponibles en este equipo; se excluyeron. Consulta su motivo en la lista.");
        ExecutableSearch = "";
        ExecutableCategory = "Todas las áreas";
        ExecutableStatus = id == "clear" ? "Todos los estados" : SelectedTweakCount == 0 ? "No disponibles" : "Seleccionados";
        SelectedExecutableTweak = ExecutableChoices.FirstOrDefault(t => t.IsSelected) ?? ExecutableChoices.FirstOrDefault();
        RefreshTweaks();
    }
    public void SelectVisibleStandard()
    {
        foreach (var choice in ExecutableView.Cast<ExecutableTweakChoice>().Where(t => t.CanSelect && t.Definition.RiskLevel != TweakRisk.Advanced).ToArray()) choice.IsSelected = true;
        RefreshTweaks();
    }
    public void LoadProfile(SavedTweakProfile profile)
    {
        TweakProfiles.Validate(profile);
        changingProfile = true;
        try { foreach (var choice in ExecutableChoices) choice.IsSelected = profile.TweakIds.Contains(choice.Id) && choice.CanSelect; }
        finally { changingProfile = false; }
        ActiveProfileId = "";
        SelectedProfile = profile.Name;
        ProfileNote = $"{profile.Name}: {SelectedTweakCount} seleccionados; {profile.TweakIds.Count - SelectedTweakCount} no disponibles. El perfil solo selecciona: no ha modificado Windows.";
        RefreshTweaks();
    }
    public void RefreshTweaks() { ExecutableView.Refresh(); Changed(nameof(VisibleTweakSummary)); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void RefreshCatalog() { InstallView.Refresh(); Changed(nameof(CatalogSummary)); }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
