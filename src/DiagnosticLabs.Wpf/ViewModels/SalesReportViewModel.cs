using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Wpf.Printing;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>
/// The Sales Report: pick the days, optionally one company and one of its batches, and print. The report is only ever printed (the module has
/// no other action), so there is nothing to save on this screen.
/// </summary>
public partial class SalesReportViewModel(
    IServiceRunner runner,
    ICurrentUser user,
    IReportPreviewDialog preview,
    ILogger<SalesReportViewModel> logger) : ViewModelBase(logger)
{
    private static readonly LookupOption AnyCompany = new(0, "(all companies)");

    public const string AnyBatch = "(all batches)";

    [ObservableProperty]
    private DateTime? _dateFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime? _dateTo = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPickBatch))]
    private long _companyId;

    [ObservableProperty]
    private string _batch = AnyBatch;

    [ObservableProperty]
    private PaymentChoice _payment = PaymentChoices[0];

    public static IReadOnlyList<PaymentChoice> PaymentChoices { get; } =
    [
        new(SalesPaymentFilter.All, "All sales"),
        new(SalesPaymentFilter.DirectPayments, "Direct payments only"),
        new(SalesPaymentFilter.ChargedToCompany, "Charged to company only"),
    ];

    public ObservableCollection<LookupOption> Companies { get; } = [];

    public ObservableCollection<string> Batches { get; } = [AnyBatch];

    /// <summary>A batch belongs to a company, so the batches are offered once a company is picked.</summary>
    public bool CanPickBatch => CompanyId != 0;

    public bool CanPrint => user.Can(ModuleIds.SalesReport, ModuleAction.Print);

    partial void OnCompanyIdChanged(long value) => _ = LoadBatchesAsync(value);

    [RelayCommand]
    private async Task InitializeAsync()
    {
        var companies = await runner.RunAsync<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());
        Companies.Clear();
        Companies.Add(AnyCompany);
        foreach (var company in companies)
            Companies.Add(company);
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

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private Task PrintAsync() => RunAsync(async () =>
    {
        if (DateFrom is not { } from || DateTo is not { } to)
        {
            ShowError("Pick both dates.");
            return;
        }

        var request = new SalesReportRequest(
            DateOnly.FromDateTime(from),
            DateOnly.FromDateTime(to),
            CompanyId == 0 ? null : CompanyId,
            Batch == AnyBatch ? null : Batch,
            Payment.Value);

        var result = await runner.RunAsync<ISalesReportService, Result<SalesReportData>>(s => s.GetAsync(request));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        var report = result.Value;
        if (report.Rows.Count == 0)
        {
            ShowInfo("There are no sales in that period. Nothing to print.");
            return;
        }

        ClearMessage();
        var days = request.From == request.To ? $"{request.From:yyyy-MM-dd}" : $"{request.From:yyyy-MM-dd} to {request.To:yyyy-MM-dd}";
        var only = request.Payment == SalesPaymentFilter.All ? string.Empty : $" ({Payment.Name.ToLowerInvariant()})";
        preview.Show(SalesReportDocumentBuilder.Build(report), "Sales Report", $"Sales Report {days}{only}");
    });
}

public sealed record PaymentChoice(SalesPaymentFilter Value, string Name);
