using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One module in the permission grid: whether the user can open it, and which actions they may take.</summary>
public partial class PermissionRowViewModel(PermissionModule module) : ObservableObject
{
    private bool _loading;

    public int ModuleId { get; } = module.Id;

    public string ModuleName { get; } = module.Name;

    // Greyed out when the module has no such action (e.g. Sales Report can only be printed).
    public bool SupportsCreate { get; } = module.SupportsCreate;

    public bool SupportsEdit { get; } = module.SupportsEdit;

    public bool SupportsDelete { get; } = module.SupportsDelete;

    public bool SupportsPrint { get; } = module.SupportsPrint;

    [ObservableProperty]
    private bool _hasAccess;

    [ObservableProperty]
    private bool _allowCreate;

    [ObservableProperty]
    private bool _allowEdit;

    [ObservableProperty]
    private bool _allowDelete;

    [ObservableProperty]
    private bool _allowPrint;

    partial void OnHasAccessChanged(bool value)
    {
        // Taking access away takes every action with it.
        if (!value && !_loading)
            AllowCreate = AllowEdit = AllowDelete = AllowPrint = false;
    }

    partial void OnAllowCreateChanged(bool value) => GrantAccessIfNeeded(value);

    partial void OnAllowEditChanged(bool value) => GrantAccessIfNeeded(value);

    partial void OnAllowDeleteChanged(bool value) => GrantAccessIfNeeded(value);

    partial void OnAllowPrintChanged(bool value) => GrantAccessIfNeeded(value);

    private void GrantAccessIfNeeded(bool allowed)
    {
        if (allowed && !HasAccess)
            HasAccess = true;
    }

    public void Load(PermissionLine? line)
    {
        _loading = true;
        try
        {
            HasAccess = line is not null;
            AllowCreate = line?.AllowCreate ?? false;
            AllowEdit = line?.AllowEdit ?? false;
            AllowDelete = line?.AllowDelete ?? false;
            AllowPrint = line?.AllowPrint ?? false;
        }
        finally
        {
            _loading = false;
        }
    }

    public void GrantEverything()
    {
        HasAccess = true;
        AllowCreate = SupportsCreate;
        AllowEdit = SupportsEdit;
        AllowDelete = SupportsDelete;
        AllowPrint = SupportsPrint;
    }

    public PermissionLine? ToLine() => HasAccess ? new PermissionLine(ModuleId, AllowCreate, AllowEdit, AllowDelete, AllowPrint) : null;
}

public partial class PermissionGroupViewModel(string name, IEnumerable<PermissionRowViewModel> rows) : ObservableObject
{
    public string Name { get; } = name;

    public ObservableCollection<PermissionRowViewModel> Rows { get; } = [.. rows];

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var row in Rows)
            row.GrantEverything();
    }

    [RelayCommand]
    private void ClearAll()
    {
        foreach (var row in Rows)
            row.HasAccess = false;
    }
}

public partial class UsersViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser currentUser,
    ILogger<UsersViewModel> logger)
    : CrudViewModel<IUserService, UserListItem, UserDetails, UserInput>(
        runner, dialogs, currentUser, ModuleIds.Users, "Users", "User", hasActiveFlag: true, logger)
{
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPermissions))]
    private bool _isAdmin;

    /// <summary>The first password of a new user, or the replacement when resetting one.</summary>
    [ObservableProperty]
    private string _temporaryPassword = string.Empty;

    [ObservableProperty]
    private string _accountStatus = string.Empty;

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private LookupOption? _copyFromUser;

    public ObservableCollection<PermissionGroupViewModel> PermissionGroups { get; } = [];

    public ObservableCollection<LookupOption> UserOptions { get; } = [];

    /// <summary>Administrators can open everything, so the grid only matters for everyone else.</summary>
    public bool ShowPermissions => !IsAdmin;

    protected override string CurrentName => Username;

    private IEnumerable<PermissionRowViewModel> AllRows => PermissionGroups.SelectMany(g => g.Rows);

    public bool CanManageExisting => !IsNew && CurrentUser.Can(ModuleIds.Users, ModuleAction.Edit);

    protected override void OnRecordChanged()
    {
        OnPropertyChanged(nameof(CanManageExisting));
        OnPropertyChanged(nameof(CanSetTemporaryPassword));
    }

    public bool CanSetTemporaryPassword => IsNew;

    protected override async Task OnInitializeAsync()
    {
        var catalog = await Call<IUserService, Result<IReadOnlyList<PermissionGroup>>>(s => s.GetPermissionCatalogAsync());
        if (catalog.IsSuccess)
        {
            PermissionGroups.Clear();
            foreach (var group in catalog.Value.Where(g => !g.IsAdminGroup))
                PermissionGroups.Add(new PermissionGroupViewModel(group.Name, group.Modules.Select(m => new PermissionRowViewModel(m))));
        }

        await LoadUserOptionsAsync();
    }

    protected override Task OnSavedAsync() => LoadUserOptionsAsync();

    private async Task LoadUserOptionsAsync()
    {
        var options = await Call<IUserService, Result<IReadOnlyList<LookupOption>>>(s => s.GetUserOptionsAsync());
        UserOptions.Clear();
        if (options.IsSuccess)
            foreach (var option in options.Value)
                UserOptions.Add(option);
    }

    protected override UserInput BuildInput() => new(
        Id,
        Username,
        FullName,
        IsAdmin,
        IsActive,
        TemporaryPassword,
        [.. AllRows.Select(r => r.ToLine()).OfType<PermissionLine>()],
        RowVersion);

    protected override void ShowFields(UserDetails d)
    {
        Username = d.Username;
        FullName = d.FullName;
        IsAdmin = d.IsAdmin;
        IsActive = d.IsActive;
        RowVersion = d.RowVersion;
        TemporaryPassword = string.Empty;
        IsLocked = d.IsLocked;
        AccountStatus = DescribeAccount(d);
        foreach (var row in AllRows)
            row.Load(d.Permissions.FirstOrDefault(p => p.ModuleId == row.ModuleId));
    }

    protected override void ResetFields()
    {
        Username = FullName = string.Empty;
        IsAdmin = false;
        IsLocked = false;
        AccountStatus = string.Empty;
        CopyFromUser = null;
        foreach (var row in AllRows)
            row.Load(null);

        // Offer a ready-made temporary password so the administrator only has to read it out.
        TemporaryPassword = PasswordPolicy.Generate();
    }

    private static string DescribeAccount(UserDetails d)
    {
        var parts = new List<string>
        {
            d.LastLoginAtUtc is { } last ? $"Last signed in {last.ToLocalTime():g}." : "Has not signed in yet.",
        };

        if (d.IsLocked)
            parts.Add($"Locked until {d.LockoutEndUtc!.Value.ToLocalTime():t} after too many failed sign-ins.");

        if (d.MustChangePassword)
            parts.Add("Must choose a new password at the next sign-in.");

        return string.Join(' ', parts);
    }

    protected override string SavedMessage(UserDetails saved, bool wasNew)
    {
        if (wasNew)
            return "User created. Give them the temporary password; they choose their own when they first sign in.";

        return saved.Id == CurrentUser.UserId
            ? "Saved. Your own permissions were refreshed."
            : "Saved. Permission changes apply the next time this user signs in; ask them to close and reopen the app.";
    }

    // ---------------------------------------------------------------- password and lockout

    [RelayCommand]
    private void GeneratePassword() => TemporaryPassword = PasswordPolicy.Generate();

    [RelayCommand]
    private Task ResetPasswordAsync() => RunAsync(async () =>
    {
        var id = Id;
        var password = TemporaryPassword;
        var result = await Call<IUserService, Result>(s => s.ResetPasswordAsync(id, password));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        await ReloadAsync();
        ShowInfo("Password reset. Give the user the temporary password; they must choose a new one at their next sign-in.");
    });

    [RelayCommand]
    private Task UnlockAsync() => RunAsync(async () =>
    {
        var id = Id;
        var result = await Call<IUserService, Result>(s => s.UnlockAsync(id));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        await ReloadAsync();
        ShowInfo("Account unlocked.");
    });

    // ---------------------------------------------------------------- permission helpers

    /// <summary>Copies another user's permissions into the grid (not saved until Save is pressed).</summary>
    [RelayCommand]
    private Task CopyPermissionsAsync() => RunAsync(async () =>
    {
        if (CopyFromUser is not { } source)
            return;

        var result = await Call<IUserService, Result<UserDetails>>(s => s.GetAsync(source.Id));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        foreach (var row in AllRows)
            row.Load(result.Value.Permissions.FirstOrDefault(p => p.ModuleId == row.ModuleId));

        ShowInfo($"Copied permissions from {source.Name}. Press Save to keep them.");
    });
}
