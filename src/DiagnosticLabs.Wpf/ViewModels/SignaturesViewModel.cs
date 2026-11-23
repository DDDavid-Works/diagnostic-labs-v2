using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One person of a signatory list on the Signatures screen.</summary>
public sealed partial class SignatoryItem(long entryId, string name, string? license) : ObservableObject
{
    public long EntryId { get; } = entryId;

    public string Name { get; } = name;

    public string License { get; } = string.IsNullOrWhiteSpace(license) ? "(no licence number)" : $"License No: {license}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSignature))]
    private ImageSource? _signature;

    public bool HasSignature => Signature is not null;
}

/// <summary>One signatory list (Medical Technologists, Pathologists, Physicians) and its people.</summary>
public sealed class SignatoryGroup(string title, EntryField field)
{
    public string Title { get; } = title;

    public EntryField Field { get; } = field;

    public ObservableCollection<SignatoryItem> People { get; } = [];
}

/// <summary>
/// Administration screen: the scanned signature of every medical technologist, pathologist and physician, printed above their names on the
/// results. A picked picture is made transparent, trimmed and shrunk before it is saved. The names and licences themselves are kept in the lists
/// of the Entry Builder ("Edit list..." opens it).
/// </summary>
public partial class SignaturesViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    IFileDialogService files,
    IEntryBuilderDialog entryBuilder,
    ILogger<SignaturesViewModel> logger) : ViewModelBase(logger)
{
    public IReadOnlyList<SignatoryGroup> Groups { get; } =
    [
        new("Medical Technologists", EntryFields.MedicalTechnologist),
        new("Pathologists", EntryFields.Pathologist),
        new("Physicians", EntryFields.Physician),
    ];

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadAsync);

    private async Task LoadAsync()
    {
        var result = await runner.RunAsync<ISignatureService, Result<IReadOnlyList<SignatoryRow>>>(s => s.GetSignatoriesAsync());
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        foreach (var group in Groups)
        {
            group.People.Clear();
            foreach (var row in result.Value.Where(r => r.ListName == group.Field.Name))
                group.People.Add(new SignatoryItem(row.EntryId, row.Name, row.LicenseNo) { Signature = ImageLoader.FromBytes(row.Signature) });
        }

        OnPropertyChanged(nameof(Groups));
    }

    [RelayCommand]
    private Task UploadAsync(SignatoryItem? person) => RunAsync(async () =>
    {
        if (person is null || files.PickImage() is not { } file)
            return;

        var png = SignatureImageProcessor.Prepare(file.Content);
        if (png is null)
        {
            ShowError($"{file.Name} could not be read as a picture of a signature (it is not a picture, or it has no ink on it).");
            return;
        }

        var result = await runner.RunAsync<ISignatureService, Result>(s => s.SetAsync(person.EntryId, png));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        person.Signature = ImageLoader.FromBytes(png);
        ShowInfo($"The signature of {person.Name} was saved. It is printed above the name on the results from now on, also when an older result is printed again.");
    });

    [RelayCommand]
    private Task RemoveAsync(SignatoryItem? person) => RunAsync(async () =>
    {
        if (person is null || !person.HasSignature
            || !dialogs.Confirm($"Remove the signature of {person.Name}? The results print without it from now on.", "Remove signature"))
            return;

        var result = await runner.RunAsync<ISignatureService, Result>(s => s.RemoveAsync(person.EntryId));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        person.Signature = null;
        ShowInfo($"The signature of {person.Name} was removed.");
    });

    /// <summary>Adds, renames or removes people of a list (with their licence numbers) in the Entry Builder, then shows the list again.</summary>
    [RelayCommand]
    private Task EditListAsync(SignatoryGroup? group) => RunAsync(async () =>
    {
        if (group is not null && entryBuilder.EditSingleLine(group.Field, ModuleIds.Signatures))
            await LoadAsync();
    });
}
