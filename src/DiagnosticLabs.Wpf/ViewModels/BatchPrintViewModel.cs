using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Wpf.Printing;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One patient in the search results: whether the result has been inputted and the day it was.</summary>
public sealed class BatchPrintRowItem(BatchPrintRow row)
{
    public string PatientName { get; } = row.PatientName;

    public long? ResultId { get; } = row.ResultId;

    public bool HasResult { get; } = row.HasResult;

    public string ResultText => HasResult ? "Yes" : "No";

    public string DateText { get; } = row.ResultDate?.ToString("dd MMM yyyy") ?? string.Empty;
}

/// <summary>
/// Batch Print: pick a company and a service (both required), optionally one of its batches and a range of days, and Search. The results grid lists
/// every patient of the batch who has the service, with whether the result has been inputted and when. Print then prints the results the grid shows
/// (one page per patient, in the grid's order); patients without a result have no page.
/// </summary>
public partial class BatchPrintViewModel(
    IServiceRunner runner,
    ICurrentUser user,
    IReportPreviewDialog preview,
    ILogger<BatchPrintViewModel> logger) : ViewModelBase(logger)
{
    public const string AnyBatch = "(all batches)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPickBatch))]
    private long _companyId;

    [ObservableProperty]
    private string _batch = AnyBatch;

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private long _serviceId;

    [ObservableProperty]
    private string _summary = string.Empty;

    public ObservableCollection<LookupOption> Companies { get; } = [];

    public ObservableCollection<string> Batches { get; } = [AnyBatch];

    public ObservableCollection<BatchPrintServiceOption> Services { get; } = [];

    /// <summary>The patients the last search found.</summary>
    public ObservableCollection<BatchPrintRowItem> Rows { get; } = [];

    public bool HasRows => Rows.Count > 0;

    /// <summary>A batch belongs to a company, so the batches are offered once a company is picked.</summary>
    public bool CanPickBatch => CompanyId != 0;

    public bool CanUse => user.Can(ModuleIds.BatchPrint, ModuleAction.Print);

    /// <summary>Printing needs at least one result in the grid.</summary>
    public bool CanPrint => CanUse && Rows.Any(r => r.HasResult);

    partial void OnCompanyIdChanged(long value)
    {
        ClearOutcome();
        _ = LoadBatchesAsync(value);
    }

    partial void OnBatchChanged(string value) => ClearOutcome();

    partial void OnDateFromChanged(DateTime? value) => ClearOutcome();

    partial void OnDateToChanged(DateTime? value) => ClearOutcome();

    partial void OnServiceIdChanged(long value) => ClearOutcome();

    /// <summary>The grid and what was said belong to the parameters they were found with.</summary>
    private void ClearOutcome()
    {
        ClearMessage();
        Summary = string.Empty;
        if (Rows.Count == 0)
            return;

        Rows.Clear();
        RowsChanged();
    }

    private void RowsChanged()
    {
        OnPropertyChanged(nameof(HasRows));
        PrintCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        var companies = await runner.RunAsync<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());
        Companies.Clear();
        foreach (var company in companies)
            Companies.Add(company);

        var services = await runner.RunAsync<IBatchPrintService, IReadOnlyList<BatchPrintServiceOption>>(s => s.GetServicesAsync());
        Services.Clear();
        foreach (var service in services)
            Services.Add(service);
    }

    private async Task LoadBatchesAsync(long companyId)
    {
        var names = companyId == 0
            ? []
            : await runner.RunAsync<IReferenceLookups, IReadOnlyList<string>>(s => s.GetBatchesAsync(companyId));

        // The company may have been changed again while the batches were loading.
        if (companyId != CompanyId)
            return;

        Batches.Clear();
        Batches.Add(AnyBatch);
        foreach (var name in names)
            Batches.Add(name);
        Batch = AnyBatch;
    }

    /// <summary>The range is optional: empty dates mean every result.</summary>
    [RelayCommand]
    private void ClearDates()
    {
        DateFrom = null;
        DateTo = null;
    }

    [RelayCommand(CanExecute = nameof(CanUse))]
    private Task SearchAsync() => RunAsync(async () =>
    {
        ClearOutcome();
        var request = new BatchPrintRequest(
            CompanyId,
            ServiceId,
            Batch == AnyBatch ? null : Batch,
            DateFrom is { } from ? DateOnly.FromDateTime(from) : null,
            DateTo is { } to ? DateOnly.FromDateTime(to) : null);

        var result = await runner.RunAsync<IBatchPrintService, Result<BatchPrintSearchResult>>(s => s.SearchAsync(request));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        foreach (var row in result.Value.Rows)
            Rows.Add(new BatchPrintRowItem(row));
        RowsChanged();

        if (Rows.Count == 0)
        {
            ShowInfo("No patient of that company has this service in that selection.");
            return;
        }

        var inputted = Rows.Count(r => r.HasResult);
        Summary = $"{Rows.Count} patient{(Rows.Count == 1 ? string.Empty : "s")}: {inputted} with results, {Rows.Count - inputted} without";
    });

    /// <summary>Prints the results the grid shows, in the grid's order.</summary>
    [RelayCommand(CanExecute = nameof(CanPrint))]
    private Task PrintAsync() => RunAsync(async () =>
    {
        var ids = Rows.Where(r => r.ResultId is not null).Select(r => r.ResultId!.Value).ToList();
        var serviceId = ServiceId;
        var result = await runner.RunAsync<IBatchPrintService, Result<IReadOnlyList<PrintableReport>>>(s => s.GetPrintablesAsync(serviceId, ids));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        ClearMessage();
        var service = Services.FirstOrDefault(s => s.ServiceId == serviceId)?.Name ?? "Results";
        var company = Companies.FirstOrDefault(c => c.Id == CompanyId)?.Name;
        var name = string.Join(" - ", new[] { "Batch Print", company, Batch == AnyBatch ? null : Batch, service }.Where(s => !string.IsNullOrWhiteSpace(s)));
        preview.Show(ReportDocumentBuilder.Build(result.Value), $"Batch Print - {service}", name);
    });
}
