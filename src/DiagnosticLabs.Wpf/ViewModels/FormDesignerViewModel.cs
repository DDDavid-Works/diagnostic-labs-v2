using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One choice of a header cell: a patient field, or "(empty)" for a cell that stays blank.</summary>
public sealed record HeaderOption(HeaderField? Field, string Label)
{
    public static readonly HeaderOption Empty = new(null, "(empty)");

    /// <summary>The empty choice first, then the fields in a fixed order; the same instances are reused so a ComboBox can match its selection.</summary>
    public static readonly IReadOnlyList<HeaderOption> All =
    [
        Empty,
        .. Enum.GetValues<HeaderField>().Select(f => new HeaderOption(f, FormDefinition.DisplayName(f))),
    ];

    public static HeaderOption For(HeaderField? field) => All.First(o => o.Field == field);
}

/// <summary>One cell of the header grid. It only offers the fields no other cell uses (its own choice stays in the list).</summary>
public sealed partial class HeaderCellViewModel(Action changed) : ObservableObject
{
    private HeaderOption _selected = HeaderOption.Empty;

    public ObservableCollection<HeaderOption> Options { get; } = [];

    public HeaderOption Selected
    {
        get => _selected;
        set
        {
            // A ComboBox pushes null while its list is being changed; the cell keeps what it had. A field another cell uses is not in its list.
            if (value is null || ReferenceEquals(value, _selected) || !Options.Contains(value))
                return;

            _selected = value;
            OnPropertyChanged();
            changed();
        }
    }

    /// <summary>Sets the choice without telling the form (used while a design is being shown).</summary>
    public void Load(HeaderField? field)
    {
        _selected = HeaderOption.For(field);
        OnPropertyChanged(nameof(Selected));
    }
}

public sealed record RoleOption(SignatoryRole Role, string Label)
{
    public static readonly IReadOnlyList<RoleOption> All = [.. Enum.GetValues<SignatoryRole>().Select(r => new RoleOption(r, FormDefinition.DisplayName(r)))];

    public static RoleOption For(SignatoryRole role) => All.First(o => o.Role == role);
}

public sealed partial class SignatoryRowViewModel(RoleOption role) : ObservableObject
{
    [ObservableProperty]
    private RoleOption _role = role;

    public IReadOnlyList<RoleOption> Roles => RoleOption.All;
}

/// <summary>A test row or a section heading of a group.</summary>
public sealed partial class RowViewModel(Guid id, bool isSection, string name) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty]
    private bool _isSection = isSection;

    [ObservableProperty]
    private string _name = name;

    /// <summary>The group the row is in (set when it is added); rows are only reordered inside their group.</summary>
    public GroupViewModel? Owner { get; set; }

    public RowDefinition ToDefinition() => new(Id, IsSection ? RowKind.Section : RowKind.Test, Name ?? string.Empty);
}

/// <summary>One column group of the body: an optional title, the reference column switch and up to 30 rows that can be dragged into a new order.</summary>
public sealed partial class GroupViewModel : ObservableObject
{
    private readonly Action? _changed;

    public GroupViewModel(Guid id, string? title, bool showReference, Action? changed = null)
    {
        Id = id;
        _changed = changed;
        _title = title ?? string.Empty;
        _showReference = showReference;
        Rows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(RowCountText));
            AddRowCommand.NotifyCanExecuteChanged();
            AddSectionCommand.NotifyCanExecuteChanged();
            _changed?.Invoke();
        };
    }

    partial void OnTitleChanged(string value) => _changed?.Invoke();

    public Guid Id { get; }

    public ObservableCollection<RowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _showReference;

    /// <summary>"Group 1" and so on, set by the form when groups are added or removed.</summary>
    [ObservableProperty]
    private string _label = string.Empty;

    public string RowCountText => $"{Rows.Count} of {BodyDefinition.MaxRowsPerGroup} rows";

    public bool CanAddRow => Rows.Count < BodyDefinition.MaxRowsPerGroup;

    public void Load(IEnumerable<RowDefinition> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
            Add(new RowViewModel(row.Id, row.Kind == RowKind.Section, row.Name));
    }

    private RowViewModel Add(RowViewModel row)
    {
        row.Owner = this;
        Rows.Add(row);
        return row;
    }

    [RelayCommand(CanExecute = nameof(CanAddRow))]
    private void AddRow()
    {
        if (CanAddRow)
            Add(new RowViewModel(Guid.NewGuid(), false, string.Empty));
    }

    [RelayCommand(CanExecute = nameof(CanAddRow))]
    private void AddSection()
    {
        if (CanAddRow)
            Add(new RowViewModel(Guid.NewGuid(), true, string.Empty));
    }

    [RelayCommand]
    private void RemoveRow(RowViewModel? row)
    {
        if (row is not null)
            Rows.Remove(row);
    }

    /// <summary>Puts <paramref name="row"/> where <paramref name="target"/> is (dragging a row onto another one). Rows of another group are ignored.</summary>
    public void MoveRow(RowViewModel row, RowViewModel target)
    {
        var from = Rows.IndexOf(row);
        var to = Rows.IndexOf(target);
        if (from < 0 || to < 0 || from == to)
            return;

        Rows.Move(from, to);
    }

    public GroupDefinition ToDefinition() => new(Id, Title, ShowReference, [.. Rows.Select(r => r.ToDefinition())]);
}

public sealed partial class TextboxViewModel(Guid id, string label) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty]
    private string _label = label;
}

/// <summary>The Custom Lab Form designer: a list of the client's own result forms and the editor for one (name, service, header grid, footer).</summary>
public partial class FormDesignerViewModel : ViewModelBase, IDirtyForm
{
    private readonly IServiceRunner _runner;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    private string? _baseline;
    private long _formId;
    private int _baseVersion;
    private bool _selectionChangeIsOurs;

    public FormDesignerViewModel(IServiceRunner runner, ICurrentUser currentUser, IDialogService dialogs, INavigationService navigation, ILogger<FormDesignerViewModel> logger)
        : base(logger)
    {
        _runner = runner;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _navigation = navigation;
        Groups.CollectionChanged += (_, _) => BodyChanged();
        Textboxes.CollectionChanged += (_, _) => BodyChanged();

        for (var i = 0; i < FormDefinition.HeaderCellCount; i++)
            Cells.Add(new HeaderCellViewModel(OnHeaderChanged));

        ShowDesign(FormDefinition.NewForm());
        RefreshHeaderOptions();
        _baseline = Snapshot();
    }

    public ObservableCollection<FormSummary> Forms { get; } = [];

    public ObservableCollection<ServiceChoice> Services { get; } = [];

    public ObservableCollection<HeaderCellViewModel> Cells { get; } = [];

    public ObservableCollection<SignatoryRowViewModel> Signatories { get; } = [];

    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    public ObservableCollection<TextboxViewModel> Textboxes { get; } = [];

    /// <summary>Set when the design would run past one printed page (worked out from the same page numbers the printout is laid out with).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFitWarning))]
    private string _fitWarning = string.Empty;

    public bool HasFitWarning => FitWarning.Length > 0;

    partial void OnShowLicenseNumberChanged(bool value) => RefreshFit();

    partial void OnShowRemarksChanged(bool value) => RefreshFit();

    /// <summary>Warns, but does not stop a save: rows count as one line and text boxes as empty, so long texts can still push a page over.</summary>
    private void RefreshFit()
    {
        if (Cells.Count != FormDefinition.HeaderCellCount)
            return;

        var overflow = CustomFormPage.Overflow(BuildDefinition());
        FitWarning = overflow <= 0
            ? string.Empty
            : $"This form will not fit on one printed page: it runs about {Math.Ceiling(overflow / CustomFormPage.RowHeight):0} rows too long. Take out some rows, a text box or the Remarks box, or switch off the licence numbers.";
    }

    public bool CanAddGroup => CanEdit && Groups.Count < BodyDefinition.MaxGroups;

    public bool CanAddTextbox => CanEdit && Textboxes.Count < BodyDefinition.MaxTextboxes;

    [RelayCommand(CanExecute = nameof(CanAddGroup))]
    private void AddGroup()
    {
        if (CanAddGroup)
            Groups.Add(new GroupViewModel(Guid.NewGuid(), null, true, RefreshFit));
    }

    [RelayCommand]
    private void RemoveGroup(GroupViewModel? group)
    {
        if (group is not null && CanEdit)
            Groups.Remove(group);
    }

    [RelayCommand(CanExecute = nameof(CanAddTextbox))]
    private void AddTextbox()
    {
        if (CanAddTextbox)
            Textboxes.Add(new TextboxViewModel(Guid.NewGuid(), string.Empty));
    }

    [RelayCommand]
    private void RemoveTextbox(TextboxViewModel? box)
    {
        if (box is not null && CanEdit)
            Textboxes.Remove(box);
    }

    private void BodyChanged()
    {
        for (var i = 0; i < Groups.Count; i++)
            Groups[i].Label = $"Group {i + 1}";

        AddGroupCommand.NotifyCanExecuteChanged();
        AddTextboxCommand.NotifyCanExecuteChanged();
        RefreshFit();
        OnPropertyChanged(nameof(CanAddGroup));
        OnPropertyChanged(nameof(CanAddTextbox));
    }

    [ObservableProperty]
    private FormSummary? _selectedForm;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private ServiceChoice? _selectedService;

    [ObservableProperty]
    private bool _showLicenseNumber = true;

    [ObservableProperty]
    private bool _showRemarks = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(IsSwitchedOff), nameof(ToggleActiveText), nameof(Heading), nameof(CanPickService), nameof(CanAddSignatory))]
    private bool _isActiveForm = true;

    public bool IsNew => _formId == 0;

    public bool IsSwitchedOff => !IsNew && !IsActiveForm;

    public string Heading => IsNew ? "New Form" : $"{FormName} (version {_baseVersion})";

    public string ToggleActiveText => IsActiveForm ? "Switch Off" : "Switch On";

    public bool CanCreate => _currentUser.Can(ModuleIds.FormDesigner, ModuleAction.Create);

    /// <summary>Whether the form on screen can be changed and saved by this user (a switched-off form is read only).</summary>
    public bool CanEdit => IsActiveForm && _currentUser.Can(ModuleIds.FormDesigner, IsNew ? ModuleAction.Create : ModuleAction.Edit);

    public bool CanSwitch => !IsNew && _currentUser.Can(ModuleIds.FormDesigner, ModuleAction.Edit);

    /// <summary>The service can be chosen while editing, and on a switched-off form (switching it on needs a free service).</summary>
    public bool CanPickService => CanEdit || (IsSwitchedOff && CanSwitch);

    public bool CanAddSignatory => CanEdit && Signatories.Count < FormDefinition.MaxSignatories;

    partial void OnFormNameChanged(string value) => OnPropertyChanged(nameof(Heading));

    partial void OnSelectedFormChanged(FormSummary? oldValue, FormSummary? newValue)
    {
        if (_selectionChangeIsOurs || newValue is null)
            return;

        _ = OpenAsync(oldValue, newValue);
    }

    // ---------------------------------------------------------------- commands

    [RelayCommand]
    private Task InitializeAsync() => RunAsync(async () =>
    {
        await LoadListAsync();
        await LoadServicesAsync(null);
        ClearMessage();
    });

    [RelayCommand]
    private async Task NewFormAsync()
    {
        if (!await ConfirmLeaveAsync())
            return;

        await RunAsync(async () =>
        {
            SetSelection(null);
            _formId = 0;
            _baseVersion = 0;
            FormName = string.Empty;
            IsActiveForm = true;
            ShowDesign(FormDefinition.NewForm());
            await LoadServicesAsync(null);
            SelectedService = null;
            FinishLoading();
            ClearMessage();
        });
    }

    [RelayCommand]
    private Task RevertAsync() => RunAsync(async () =>
    {
        if (IsNew)
        {
            FormName = string.Empty;
            SelectedService = null;
            ShowDesign(FormDefinition.NewForm());
        }
        else
        {
            await ShowFormAsync(_formId);
        }

        FinishLoading();
        ShowInfo("Changes discarded.");
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(SaveCoreAsync);

    [RelayCommand]
    private Task ToggleActiveAsync() => RunAsync(async () =>
    {
        if (IsActiveForm && !_dialogs.Confirm(
                "Switch this form off? It disappears from the menu and from the registration list, and its service can be used by another form. Results already saved stay.",
                "Custom Lab Form"))
            return;

        var turnOn = !IsActiveForm;
        var result = await _runner.RunAsync<IFormDesignerService, Result<FormDetails>>(s => s.SetActiveAsync(_formId, turnOn, SelectedService?.Id));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        await LoadListAsync();
        Show(result.Value);
        await LoadServicesAsync(_formId);
        SelectedService = Services.FirstOrDefault(s => s.Id == result.Value.ServiceId);
        FinishLoading();
        _navigation.MenuChanged();
        ShowInfo(turnOn ? "The form is switched on." : "The form is switched off.");
    });

    [RelayCommand(CanExecute = nameof(CanAddSignatory))]
    private void AddSignatory()
    {
        var technologists = Signatories.Count(s => s.Role.Role == SignatoryRole.MedicalTechnologist);
        var role = technologists < FormDefinition.MaxMedicalTechnologists ? SignatoryRole.MedicalTechnologist : SignatoryRole.Pathologist;
        Signatories.Add(new SignatoryRowViewModel(RoleOption.For(role)));
        SignatoriesChanged();
    }

    [RelayCommand]
    private void RemoveSignatory(SignatoryRowViewModel? row)
    {
        if (row is null || !CanEdit)
            return;

        Signatories.Remove(row);
        SignatoriesChanged();
    }

    // ---------------------------------------------------------------- saving and leaving

    private async Task SaveCoreAsync()
    {
        if (!CanEdit)
            return;

        var input = new FormInput(_formId, FormName, SelectedService?.Id ?? 0, BuildDefinition(), _baseVersion);
        var wasNew = IsNew;
        var versionBefore = _baseVersion;
        var result = await _runner.RunAsync<IFormDesignerService, Result<FormDetails>>(s => s.SaveAsync(input));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        await LoadListAsync();
        Show(result.Value);
        await LoadServicesAsync(_formId);
        SelectedService = Services.FirstOrDefault(s => s.Id == result.Value.ServiceId);
        FinishLoading();
        _navigation.MenuChanged();
        ShowInfo(wasNew
            ? "The form was created and is in the menu now. Give other users access to it in Users."
            : result.Value.VersionNumber > versionBefore ? $"Saved as version {result.Value.VersionNumber}." : "Saved.");
    }

    public bool IsDirty => _baseline is not null && Snapshot() != _baseline;

    public async Task<bool> ConfirmLeaveAsync()
    {
        if (!IsDirty)
            return true;

        if (!CanEdit)
            return _dialogs.Confirm("The form has changes, but you are not allowed to save them. Leave without saving?", "Custom Lab Form");

        switch (_dialogs.AskToSave("The form has changes that are not saved. Do you want to save them?", "Custom Lab Form"))
        {
            case SaveChoice.Save:
                await RunAsync(SaveCoreAsync);
                return !IsDirty;
            case SaveChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    // ---------------------------------------------------------------- showing a form

    private async Task OpenAsync(FormSummary? previous, FormSummary next)
    {
        if (next.Id == _formId)
            return;

        if (!await ConfirmLeaveAsync())
        {
            SetSelection(previous);
            return;
        }

        await RunAsync(async () =>
        {
            await ShowFormAsync(next.Id);
            FinishLoading();
            ClearMessage();
        });
    }

    private async Task ShowFormAsync(long id)
    {
        var result = await _runner.RunAsync<IFormDesignerService, Result<FormDetails>>(s => s.GetAsync(id));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Show(result.Value);
        await LoadServicesAsync(id);
        SelectedService = Services.FirstOrDefault(s => s.Id == result.Value.ServiceId);
    }

    private void Show(FormDetails details)
    {
        _formId = details.Id;
        _baseVersion = details.VersionNumber;
        FormName = details.Name;
        IsActiveForm = details.IsActive;
        ShowDesign(details.Definition);
        SetSelection(Forms.FirstOrDefault(f => f.Id == details.Id));
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSwitch));
        OnPropertyChanged(nameof(CanPickService));
    }

    private void ShowDesign(FormDefinition design)
    {
        for (var i = 0; i < Cells.Count; i++)
            Cells[i].Load(i < design.Header.Count ? design.Header[i] : null);

        RefreshHeaderOptions();
        Signatories.Clear();
        foreach (var slot in design.Footer.Signatories)
            Signatories.Add(new SignatoryRowViewModel(RoleOption.For(slot.Role)));

        ShowLicenseNumber = design.Footer.ShowLicenseNumber;
        ShowRemarks = design.Footer.ShowRemarks;
        SignatoriesChanged();

        Groups.Clear();
        foreach (var group in design.Body.Groups)
        {
            var shown = new GroupViewModel(group.Id, group.Title, group.ShowReference, RefreshFit);
            shown.Load(group.Rows);
            Groups.Add(shown);
        }

        Textboxes.Clear();
        foreach (var box in design.Body.Textboxes)
            Textboxes.Add(new TextboxViewModel(box.Id, box.Label));
    }

    /// <summary>Called once a form (or a blank one) is on screen: what is shown now is the "unchanged" state.</summary>
    private void FinishLoading()
    {
        _baseline = Snapshot();
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(Heading));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSwitch));
        OnPropertyChanged(nameof(CanCreate));
        OnPropertyChanged(nameof(IsSwitchedOff));
        OnPropertyChanged(nameof(CanPickService));
        SignatoriesChanged();
    }

    private async Task LoadListAsync()
    {
        var result = await _runner.RunAsync<IFormDesignerService, Result<IReadOnlyList<FormSummary>>>(s => s.ListAsync());
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        var current = _formId;
        Forms.Clear();
        foreach (var form in result.Value)
            Forms.Add(form);

        SetSelection(Forms.FirstOrDefault(f => f.Id == current));
    }

    private async Task LoadServicesAsync(long? formId)
    {
        var result = await _runner.RunAsync<IFormDesignerService, Result<IReadOnlyList<ServiceChoice>>>(s => s.GetServiceChoicesAsync(formId));
        Services.Clear();
        if (result.IsFailure)
            return;

        foreach (var choice in result.Value)
            Services.Add(choice);
    }

    private void SetSelection(FormSummary? form)
    {
        _selectionChangeIsOurs = true;
        try
        {
            SelectedForm = form;
        }
        finally
        {
            _selectionChangeIsOurs = false;
        }
    }

    // ---------------------------------------------------------------- the design on screen

    private FormDefinition BuildDefinition() => new(
        [.. Cells.Select(c => c.Selected.Field)],
        new FooterDefinition([.. Signatories.Select(s => new SignatorySlot(s.Role.Role))], ShowLicenseNumber, ShowRemarks))
    {
        Body = new BodyDefinition([.. Groups.Select(g => g.ToDefinition())], [.. Textboxes.Select(t => new TextboxDefinition(t.Id, t.Label ?? string.Empty))]),
    };

    private void OnHeaderChanged()
    {
        RefreshHeaderOptions();
        OnPropertyChanged(nameof(IsDirty));
    }

    private void SignatoriesChanged()
    {
        AddSignatoryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddSignatory));
        BodyChanged(); // the group and text box buttons depend on whether the form can be edited too
    }

    /// <summary>Each cell offers "(empty)", its own field and the fields no other cell uses. Items are added and removed one by one so the selection is never lost.</summary>
    private void RefreshHeaderOptions()
    {
        var used = Cells.Select(c => c.Selected.Field).Where(f => f is not null).Select(f => f!.Value).ToHashSet();
        foreach (var cell in Cells)
        {
            var wanted = HeaderOption.All.Where(o => o.Field is null || o.Field == cell.Selected.Field || !used.Contains(o.Field.Value)).ToList();
            foreach (var option in cell.Options.Where(o => !wanted.Contains(o)).ToList())
                cell.Options.Remove(option);

            for (var i = 0; i < wanted.Count; i++)
            {
                if (i >= cell.Options.Count || !ReferenceEquals(cell.Options[i], wanted[i]))
                    cell.Options.Insert(i, wanted[i]);
            }
        }
    }

    private string Snapshot() => string.Join(
        "\u001f",
        _formId,
        FormName?.Trim(),
        IsActiveForm ? SelectedService?.Id : null, // on a switched-off form the service is only picked to switch it on again
        BuildDefinition().ToJson());
}
