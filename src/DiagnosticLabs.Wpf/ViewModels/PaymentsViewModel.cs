using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Billing;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Payments;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>
/// Payments: find the registration (by code, or by typing a name), see what is owed, give a discount if needed and
/// take a payment, or charge the registration to its company. The amount is pre-filled with the balance for speed.
/// </summary>
public partial class PaymentsViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    ILogger<PaymentsViewModel> logger)
    : CrudViewModel<IPaymentService, PaymentListItem, PaymentDetails, PaymentInput>(
        runner, dialogs, user, ModuleIds.Payments, "Payments", "Payment", hasActiveFlag: false, logger)
{
    private static readonly LookupOption AnyCompany = new(0, "(any company)");
    private static readonly DiscountOption CustomDiscount = new(0, "(none / type a discount)", []);

    private readonly ILogger _log = logger;

    // While true, field changes come from loading or resetting the form and must not trigger recalculation side effects.
    private bool _loading;

    // The payment amount follows the balance until the cashier types an amount.
    private bool _amountEdited;
    private bool _settingAmount;
    private int _suggestVersion;
    private int _registrationVersion;
    private long _requestedRegistrationId;

    // ----- the registration being paid -----
    [ObservableProperty]
    private string _findText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRegistration), nameof(Price), nameof(DiscountTotal), nameof(AmountDue), nameof(Paid), nameof(BalanceBefore), nameof(BalanceAfter), nameof(RegistrationNote), nameof(CanChangeRegistration))]
    private RegistrationBalance? _registration;

    [ObservableProperty]
    private RegistrationMatch? _selectedSuggestion;

    // ----- the payment -----
    [ObservableProperty]
    private DateTime? _paymentDate = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountTotal), nameof(AmountDue), nameof(BalanceBefore), nameof(BalanceAfter))]
    private bool _discountIsPercentage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiscountTotal), nameof(AmountDue), nameof(BalanceBefore), nameof(BalanceAfter))]
    private decimal _discountValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceAfter))]
    private decimal _paymentAmount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceAfter), nameof(CanEditAmount))]
    private bool _isCharge;

    // A maintained discount (PWD, Senior Citizen...) fills in the value below and locks it; "(none / type a discount)" unlocks it.
    // Nullable because a combo box writes null into its selection while its items are being swapped.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTypeDiscount), nameof(HasDiscountSteps), nameof(DiscountTotal), nameof(AmountDue), nameof(BalanceBefore), nameof(BalanceAfter))]
    private long? _selectedDiscountId = 0;

    // ----- list filters -----
    [ObservableProperty]
    private long _companyFilterId;

    [ObservableProperty]
    private DateTime? _dateFilter;

    public ObservableCollection<LookupOption> FilterCompanies { get; } = [];

    public ObservableCollection<RegistrationMatch> Suggestions { get; } = [];

    public ObservableCollection<DiscountOption> Discounts { get; } = [CustomDiscount];

    /// <summary>The details of the chosen maintained discount, in the order they are applied, with what each takes off.</summary>
    public ObservableCollection<DiscountStepRow> DiscountSteps { get; } = [];

    public bool HasDiscountSteps => SelectedDiscountId is > 0;

    public bool CanTypeDiscount => SelectedDiscountId is null or 0;

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool HasRegistration => Registration is not null;

    /// <summary>A saved payment stays with its registration; a new one can be pointed at another.</summary>
    public bool CanChangeRegistration => IsNew && HasRegistration;

    public bool CanEditAmount => !IsCharge;

    public IReadOnlyList<PaymentServiceLine> ServiceLines => Registration?.Services ?? [];

    public decimal Price => Registration?.Price ?? 0m;

    public decimal DiscountTotal => HasDiscountSteps
        ? BillingMath.DiscountTotal(Price, CurrentSteps())
        : BillingMath.DiscountTotal(Price, DiscountIsPercentage ? null : DiscountValue, DiscountIsPercentage ? DiscountValue : null);

    public decimal AmountDue => Price - DiscountTotal;

    /// <summary>Paid by the other payments on this registration (not this one).</summary>
    public decimal Paid => Registration?.Paid ?? 0m;

    public decimal BalanceBefore => Registration?.IsCharged == true ? 0m : Math.Max(BillingMath.Round(AmountDue - Paid), 0m);

    public decimal BalanceAfter => IsCharge || Registration?.IsCharged == true ? 0m : BillingMath.Round(AmountDue - Paid - PaymentAmount);

    public string RegistrationNote => Registration switch
    {
        null => string.Empty,
        { IsCharged: true } => "This registration is charged to its company.",
        _ when BalanceBefore == 0m && Paid > 0m => "Fully paid.",
        _ => string.Empty,
    };

    protected override string CurrentName => Registration is null ? "this payment" : $"the payment for {Registration.RegistrationCode}";

    protected override void OnRecordChanged() => OnPropertyChanged(nameof(CanChangeRegistration));

    // ------------------------------------------------------------------ reactions

    partial void OnFindTextChanged(string value)
    {
        if (!_loading)
            Fire(() => SuggestAsync(value));
    }

    partial void OnPaymentAmountChanged(decimal value)
    {
        if (!_settingAmount && !_loading)
            _amountEdited = true;
    }

    partial void OnIsChargeChanged(bool value)
    {
        if (_loading)
            return;

        // A charge has no amount; unticking it goes back to paying the balance.
        _amountEdited = false;
        SetAmount(value ? 0m : BalanceBefore);
    }

    partial void OnSelectedDiscountIdChanged(long? value)
    {
        RefreshDiscountSteps();
        if (!_loading)
            FollowBalance();
    }

    /// <summary>
    /// The steps the chosen maintained discount gives, in order. A registration that already has this discount shows the details it
    /// was given; otherwise they are the discount as it is now.
    /// </summary>
    private List<DiscountStep> CurrentSteps()
    {
        if (SelectedDiscountId is not > 0)
            return [];

        if (Registration is { DiscountSteps: { Count: > 0 } given } && Registration.DiscountId == SelectedDiscountId)
            return [.. given.Select(s => new DiscountStep(s.Amount, s.Percentage))];

        return [.. (Discounts.FirstOrDefault(d => d.Id == SelectedDiscountId)?.Choices ?? []).Select(c => new DiscountStep(c.Amount, c.Percentage))];
    }

    private void RefreshDiscountSteps()
    {
        DiscountSteps.Clear();
        var number = 0;
        foreach (var result in BillingMath.ApplySteps(Price, CurrentSteps()))
        {
            var what = result.Step.Percentage is { } p ? $"{p:0.##}%" : $"{result.Step.Amount:N2}";
            DiscountSteps.Add(new DiscountStepRow(++number, what, result.Cut));
        }

        OnPropertyChanged(nameof(HasDiscountSteps));
    }

    partial void OnDiscountValueChanged(decimal value) => FollowBalance();

    partial void OnDiscountIsPercentageChanged(bool value) => FollowBalance();

    partial void OnSelectedSuggestionChanged(RegistrationMatch? value)
    {
        if (value is not null)
            Fire(() => PickRegistrationAsync(value.Id));
    }

    partial void OnCompanyFilterIdChanged(long value) => SearchAgain();

    partial void OnDateFilterChanged(DateTime? value) => SearchAgain();

    private void FollowBalance()
    {
        if (!_loading && !_amountEdited && !IsCharge)
            SetAmount(BalanceBefore);
    }

    private void SearchAgain()
    {
        if (!_loading)
            Fire(() => SearchCommand.ExecuteAsync(null));
    }

    private void SetAmount(decimal value)
    {
        _settingAmount = true;
        try
        {
            PaymentAmount = value;
        }
        finally
        {
            _settingAmount = false;
        }
    }

    protected override CrudSearch CreateSearch() => new PaymentSearch(
        SearchText,
        CompanyFilterId == 0 ? null : CompanyFilterId,
        DateFilter is { } d ? DateOnly.FromDateTime(d) : null,
        Page,
        PageSize,
        IncludeInactive);

    // ------------------------------------------------------------------ the form <-> the service

    protected override PaymentInput BuildInput()
    {
        // A maintained discount gives its own details; the typed value is only for a one-off discount.
        var maintained = SelectedDiscountId is > 0;
        var percentage = !maintained && DiscountIsPercentage && DiscountValue > 0 ? DiscountValue : (decimal?)null;
        var amount = maintained || percentage is not null ? (decimal?)null : (DiscountIsPercentage ? 0m : DiscountValue);

        return new PaymentInput(
            Id,
            Registration?.RegistrationId ?? 0,
            DateOnly.FromDateTime(PaymentDate ?? DateTime.Today),
            IsCharge ? PaymentType.Charge : PaymentType.Payment,
            IsCharge ? 0m : PaymentAmount,
            amount,
            percentage,
            RowVersion,
            SelectedDiscountId is > 0 ? SelectedDiscountId : null);
    }

    protected override void ShowFields(PaymentDetails d)
    {
        _loading = true;
        try
        {
            PaymentDate = d.PaymentDate.ToDateTime(TimeOnly.MinValue);
            IsCharge = d.Type == PaymentType.Charge;
            PaymentAmount = d.Amount;
            RowVersion = d.RowVersion;
            _amountEdited = true;
            ClearSuggestions();
        }
        finally
        {
            _loading = false;
        }

        // The registration's amounts, without this payment counted twice.
        Fire(() => LoadRegistrationAsync(d.RegistrationId, d.Id, payBalance: false));
    }

    protected override void ResetFields()
    {
        _registrationVersion++;
        _loading = true;
        try
        {
            FindText = string.Empty;
            Registration = null;
            PaymentDate = DateTime.Today;
            DiscountIsPercentage = false;
            DiscountValue = 0m;
            SelectedDiscountId = 0;
            IsCharge = false;
            PaymentAmount = 0m;
            _amountEdited = false;
            ClearSuggestions();
        }
        finally
        {
            _loading = false;
        }

        NotifyAmounts();
    }

    protected override string SavedMessage(PaymentDetails saved, bool wasNew) =>
        saved.Type == PaymentType.Charge ? "Saved. The registration is charged." : "Saved successfully.";

    /// <summary>Asks the screen to open with this registration loaded (used by "Pay now" on a registration).</summary>
    public void RequestRegistration(long registrationId) => _requestedRegistrationId = registrationId;

    protected override async Task OnReadyAsync()
    {
        if (_requestedRegistrationId != 0)
            await LoadRegistrationAsync(_requestedRegistrationId, 0, payBalance: true);
    }

    protected override async Task OnInitializeAsync()
    {
        await LoadDiscountsAsync(null);
        var companies = await Call<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());
        FilterCompanies.Clear();
        FilterCompanies.Add(AnyCompany);
        foreach (var company in companies)
            FilterCompanies.Add(company);
    }

    // ------------------------------------------------------------------ finding the registration

    /// <summary>Loads the registration whose code was typed (Enter in the find box).</summary>
    [RelayCommand]
    private Task FindAsync() => RunAsync(async () =>
    {
        var text = FindText;
        var result = await Call<IPaymentService, Result<RegistrationBalance>>(s => s.FindRegistrationAsync(text));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        ClearSuggestions();
        _registrationVersion++;
        await LoadDiscountsAsync(result.Value.DiscountId);
        ApplyRegistration(result.Value, payBalance: true);
        ClearMessage();
    });

    [RelayCommand]
    private void ChangeRegistration()
    {
        _registrationVersion++;
        _loading = true;
        try
        {
            FindText = string.Empty;
            Registration = null;
            DiscountIsPercentage = false;
            DiscountValue = 0m;
            SelectedDiscountId = 0;
            IsCharge = false;
            PaymentAmount = 0m;
            _amountEdited = false;
        }
        finally
        {
            _loading = false;
        }

        NotifyAmounts();
    }

    private async Task SuggestAsync(string text)
    {
        var version = ++_suggestVersion;
        if (text.Trim().Length < PaymentService.MinSuggestionLength)
        {
            ClearSuggestions();
            return;
        }

        await Task.Delay(250); // wait for a pause in typing
        if (version != _suggestVersion)
            return;

        var result = await Call<IPaymentService, Result<IReadOnlyList<RegistrationMatch>>>(s => s.SuggestRegistrationsAsync(text));
        if (version != _suggestVersion)
            return;

        Suggestions.Clear();
        if (result.IsSuccess)
        {
            foreach (var match in result.Value)
                Suggestions.Add(match);
        }

        OnPropertyChanged(nameof(HasSuggestions));
    }

    private void ClearSuggestions()
    {
        _suggestVersion++;
        Suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    private async Task PickRegistrationAsync(long registrationId)
    {
        ClearSuggestions();
        SelectedSuggestion = null;
        await LoadRegistrationAsync(registrationId, 0, payBalance: true);
        ClearMessage();
    }

    private async Task LoadRegistrationAsync(long registrationId, long exceptPaymentId, bool payBalance)
    {
        var version = ++_registrationVersion;
        var result = await Call<IPaymentService, Result<RegistrationBalance>>(s => s.GetRegistrationAsync(registrationId, exceptPaymentId));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        await LoadDiscountsAsync(result.Value.DiscountId);

        // The form was cleared or pointed elsewhere while this was loading: drop the stale answer.
        if (version == _registrationVersion)
            ApplyRegistration(result.Value, payBalance);
    }

    /// <summary>Shows a registration: its discount becomes the form's discount and, for a new payment, the amount is the balance.</summary>
    private void ApplyRegistration(RegistrationBalance balance, bool payBalance)
    {
        _loading = true;
        try
        {
            Registration = balance;
            FindText = balance.RegistrationCode;
            DiscountIsPercentage = balance.DiscountPercentage is not null;
            DiscountValue = balance.DiscountPercentage ?? balance.DiscountAmount ?? 0m;
            SelectedDiscountId = balance.DiscountId ?? 0;
            if (payBalance)
            {
                _amountEdited = false;
                IsCharge = false;
                SetAmount(balance.Balance);
            }
        }
        finally
        {
            _loading = false;
        }

        ClearSuggestions();
        NotifyAmounts();
    }

    /// <summary>Loads the discounts on offer, plus the one a registration already has even if it was switched off since.</summary>
    private async Task LoadDiscountsAsync(long? alsoId)
    {
        if (alsoId is null && Discounts.Count > 1)
            return;

        var discounts = await Call<IReferenceLookups, IReadOnlyList<DiscountOption>>(s => s.GetDiscountsAsync(alsoId));
        if (alsoId is { } id && Discounts.Any(d => d.Id == id))
            return;

        var keep = SelectedDiscountId;
        _loading = true;
        try
        {
            Discounts.Clear();
            Discounts.Add(CustomDiscount);
            foreach (var discount in discounts)
                Discounts.Add(discount);

            SelectedDiscountId = keep ?? 0;
        }
        finally
        {
            _loading = false;
        }
    }

    private void NotifyAmounts()
    {
        RefreshDiscountSteps();
        OnPropertyChanged(nameof(ServiceLines));
        OnPropertyChanged(nameof(DiscountTotal));
        OnPropertyChanged(nameof(AmountDue));
        OnPropertyChanged(nameof(BalanceBefore));
        OnPropertyChanged(nameof(BalanceAfter));
        OnPropertyChanged(nameof(RegistrationNote));
        OnPropertyChanged(nameof(CanChangeRegistration));
    }

    // ------------------------------------------------------------------ commands

    /// <summary>Loads the registration clicked in the type-ahead list.</summary>
    [RelayCommand]
    private void PickSuggestion(RegistrationMatch? match)
    {
        if (match is not null)
            Fire(() => PickRegistrationAsync(match.Id));
    }

    /// <summary>Fills the payment amount with what is still owed.</summary>
    [RelayCommand]
    private void PayBalance()
    {
        _amountEdited = false;
        IsCharge = false;
        SetAmount(BalanceBefore);
    }

    /// <summary>Saves, then shows an empty form ready for the next payment.</summary>
    [RelayCommand]
    private Task SaveAndNewAsync() => RunAsync(async () =>
    {
        if (await SaveCoreAsync())
            BeginNewRecord(keepMessage: true);
    });

    [RelayCommand]
    private void ClearFilters()
    {
        _loading = true;
        try
        {
            SearchText = string.Empty;
            CompanyFilterId = 0;
            DateFilter = null;
        }
        finally
        {
            _loading = false;
        }

        SearchAgain();
    }

    /// <summary>Runs a background lookup without blocking the form; a failure is logged and the form keeps working.</summary>
    private async void Fire(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Background lookup failed on the payments screen");
        }
    }
}

/// <summary>One line of the muted list under a maintained discount: its position, what it is (20.00 or 3%) and what it takes off.</summary>
public sealed record DiscountStepRow(int Number, string Description, decimal Cut)
{
    public string Label => $"{Number}. {Description}";
}