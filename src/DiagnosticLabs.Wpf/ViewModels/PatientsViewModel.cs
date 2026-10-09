using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Patients;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

public partial class PatientsViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser currentUser,
    ILogger<PatientsViewModel> logger)
    : CrudViewModel<IPatientService, PatientListItem, PatientDetails, PatientInput>(
        runner, dialogs, currentUser, ModuleIds.Patients, "Patients", "Patient", hasActiveFlag: false, logger)
{
    private const string NewCodeText = "(assigned when saved)";

    public ObservableCollection<string> Genders { get; } = [];

    public ObservableCollection<string> CivilStatuses { get; } = [];

    [ObservableProperty]
    private string _patientCode = NewCodeText;

    [ObservableProperty]
    private string _patientName = string.Empty;

    [ObservableProperty]
    private DateTime? _dateOfBirth;

    [ObservableProperty]
    private string _age = string.Empty;

    [ObservableProperty]
    private string? _sex;

    [ObservableProperty]
    private string? _civilStatus;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string? _contactNumbers;

    protected override string CurrentName => PatientName;

    // The code is what uniquely finds the new patient, even if the name is common.
    protected override string SearchTextAfterCreate => PatientCode;

    partial void OnDateOfBirthChanged(DateTime? value) =>
        Age = AgeCalculator.Describe(value is { } d ? DateOnly.FromDateTime(d) : null, DateOnly.FromDateTime(DateTime.Today)) ?? string.Empty;

    protected override PatientInput BuildInput() => new(
        Id,
        PatientName,
        DateOfBirth is { } d ? DateOnly.FromDateTime(d) : null,
        Sex,
        CivilStatus,
        Address,
        ContactNumbers,
        RowVersion);

    protected override void ShowFields(PatientDetails details)
    {
        PatientCode = details.PatientCode;
        PatientName = details.PatientName;
        DateOfBirth = details.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
        Sex = details.Sex;
        CivilStatus = details.CivilStatus;
        Address = details.Address;
        ContactNumbers = details.ContactNumbers;
        RowVersion = details.RowVersion;
    }

    protected override void ResetFields()
    {
        PatientCode = NewCodeText;
        PatientName = string.Empty;
        DateOfBirth = null;
        Sex = null;
        CivilStatus = null;
        Address = string.Empty;
        ContactNumbers = null;
    }

    protected override Task OnInitializeAsync() => LoadChoicesAsync();

    protected override Task OnSavedAsync() => LoadChoicesAsync();

    private async Task LoadChoicesAsync()
    {
        var genders = await Call<ILookupService, IReadOnlyList<string>>(s => s.GetChoicesAsync(LookupFields.Sex));
        var statuses = await Call<ILookupService, IReadOnlyList<string>>(s => s.GetChoicesAsync(LookupFields.CivilStatus));
        Replace(Genders, genders);
        Replace(CivilStatuses, statuses);
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }
}
