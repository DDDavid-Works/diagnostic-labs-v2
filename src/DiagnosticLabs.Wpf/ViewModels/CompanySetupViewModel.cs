using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Settings;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>The laboratory's own details: one record, edited as a whole, with a logo.</summary>
public partial class CompanySetupViewModel(
    IServiceRunner runner,
    ICurrentUser currentUser,
    IFileDialogService files,
    ILogger<CompanySetupViewModel> logger) : ViewModelBase(logger)
{
    private byte[] _rowVersion = [];
    private long _id;
    private byte[]? _logo;

    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string _subCompanyName = string.Empty;

    [ObservableProperty]
    private string _tagline = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _contactNumbers = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo))]
    private ImageSource? _logoImage;

    public bool HasLogo => LogoImage is not null;

    public bool CanSave => currentUser.Can(ModuleIds.CompanySetup, ModuleAction.Edit);

    [RelayCommand]
    private Task InitializeAsync() => RunAsync(LoadAsync);

    [RelayCommand]
    private Task RevertAsync() => RunAsync(async () =>
    {
        await LoadAsync();
        ShowInfo("Changes discarded.");
    });

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var input = new CompanySetupInput(_id, CompanyName, SubCompanyName, Tagline, Address, ContactNumbers, Email, Code, _logo, _rowVersion);
        var result = await runner.RunAsync<ICompanySetupService, Result<CompanySetupDetails>>(s => s.SaveAsync(input));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Show(result.Value);
        ShowInfo("Saved successfully.");
    });

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void ChooseLogo()
    {
        if (files.PickImage() is not { } picked)
            return;

        var image = ImageLoader.FromBytes(picked.Content);
        if (image is null)
        {
            ShowError("That file could not be read as an image.");
            return;
        }

        _logo = picked.Content;
        LogoImage = image;
        ShowInfo($"Logo '{picked.Name}' chosen. Press Save to keep it.");
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void RemoveLogo()
    {
        _logo = null;
        LogoImage = null;
        ShowInfo("Logo removed. Press Save to keep that.");
    }

    private async Task LoadAsync()
    {
        var result = await runner.RunAsync<ICompanySetupService, Result<CompanySetupDetails>>(s => s.GetAsync());
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Show(result.Value);
        ClearMessage();
    }

    private void Show(CompanySetupDetails d)
    {
        _id = d.Id;
        _rowVersion = d.RowVersion;
        _logo = d.Logo;
        CompanyName = d.CompanyName ?? string.Empty;
        SubCompanyName = d.SubCompanyName ?? string.Empty;
        Tagline = d.Tagline ?? string.Empty;
        Address = d.Address ?? string.Empty;
        ContactNumbers = d.ContactNumbers ?? string.Empty;
        Email = d.Email ?? string.Empty;
        Code = d.Code ?? string.Empty;
        LogoImage = ImageLoader.FromBytes(d.Logo);
    }
}
