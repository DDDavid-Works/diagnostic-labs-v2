using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Billing;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Registrations;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Payments;

public sealed record PaymentListItem(
    long Id, DateOnly PaymentDate, string RegistrationCode, string PatientName, string? CompanyName, decimal Amount, PaymentType Type) : IHasId;

/// <summary>Search text (registration code, patient name or patient code) plus optional company and day filters.</summary>
public sealed record PaymentSearch(
    string? Text,
    long? CompanyId = null,
    DateOnly? Date = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize,
    bool IncludeInactive = false) : CrudSearch(Text, Page, PageSize, IncludeInactive);

public sealed record PaymentDetails(
    long Id, long RegistrationId, DateOnly PaymentDate, PaymentType Type, decimal Amount, byte[] RowVersion) : IHasId;

/// <summary>
/// One payment, plus the discount given on its registration: either a maintained discount (<see cref="DiscountId"/>, whose details are
/// applied one after another) or a typed one-off discount (a percentage or a fixed amount, never both).
/// A charge records that the registration is billed to its company: it has no amount and settles the balance.
/// </summary>
public sealed record PaymentInput(
    long Id,
    long RegistrationId,
    DateOnly Date,
    PaymentType Type,
    decimal Amount,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    byte[]? RowVersion,
    long? DiscountId = null) : ICrudInput;

public sealed record PaymentServiceLine(string ServiceName, decimal Price);

/// <summary>One step of a maintained discount as given to a registration: what it is and what it took off.</summary>
public sealed record DiscountStepLine(decimal? Amount, decimal? Percentage, decimal Cut);

/// <summary>What the payment screen shows about a registration. <see cref="Paid"/> leaves out the payment being edited.</summary>
public sealed record RegistrationBalance(
    long RegistrationId,
    string RegistrationCode,
    DateOnly Date,
    string PatientCode,
    string PatientName,
    string? CompanyName,
    string BatchName,
    IReadOnlyList<PaymentServiceLine> Services,
    decimal Price,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    decimal DiscountTotal,
    decimal AmountDue,
    decimal Paid,
    decimal Balance,
    bool IsCharged,
    long? DiscountId = null,
    IReadOnlyList<DiscountStepLine>? DiscountSteps = null);

/// <summary>A registration offered while typing in the find box.</summary>
public sealed record RegistrationMatch(long Id, string RegistrationCode, string PatientName, DateOnly Date, decimal Balance);

public interface IPaymentService : ICrudService<PaymentListItem, PaymentDetails, PaymentInput>
{
    /// <summary>The registration with this exact code, or the not-found error.</summary>
    Task<Result<RegistrationBalance>> FindRegistrationAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>A registration's amounts, leaving out <paramref name="exceptPaymentId"/> (the payment being edited).</summary>
    Task<Result<RegistrationBalance>> GetRegistrationAsync(long registrationId, long exceptPaymentId = 0, CancellationToken cancellationToken = default);

    /// <summary>Registrations whose code or patient matches the typed text, newest first.</summary>
    Task<Result<IReadOnlyList<RegistrationMatch>>> SuggestRegistrationsAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class PaymentService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : CrudService<Payment, PaymentListItem, PaymentDetails, PaymentInput>(db, currentUser, ModuleIds.Payments, "Payment"),
      IPaymentService
{
    public const int MaxSuggestions = 8;
    public const int MinSuggestionLength = 2;

    protected override DbSet<Payment> Set => Db.Payments;

    protected override IQueryable<Payment> ForListing(IQueryable<Payment> q) =>
        q.Include(p => p.PatientRegistration).ThenInclude(r => r.Patient)
            .Include(p => p.PatientRegistration).ThenInclude(r => r.Company);

    protected override IQueryable<Payment> ForEditing(IQueryable<Payment> q) =>
        q.Include(p => p.PatientRegistration).ThenInclude(r => r.Patient)
            .Include(p => p.PatientRegistration).ThenInclude(r => r.DiscountSteps);

    // What the discount will be when this save is applied, worked out while validating (see ValidateAsync).
    private IReadOnlyList<DiscountStep>? _stepsToGive;

    protected override IQueryable<Payment> Matches(IQueryable<Payment> q, string word) =>
        q.Where(p => p.PatientRegistration.RegistrationCode.Contains(word)
                     || p.PatientRegistration.Patient.PatientName.Contains(word)
                     || p.PatientRegistration.Patient.PatientCode.Contains(word));

    protected override IOrderedQueryable<Payment> Order(IQueryable<Payment> q) =>
        q.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id);

    // Soft-deleted payments are already hidden by the global query filter.
    protected override IQueryable<Payment> ApplyActiveFilter(IQueryable<Payment> q, bool includeInactive) => q;

    protected override void Remove(Payment entity) => Set.Remove(entity);

    protected override IQueryable<Payment> Filter(IQueryable<Payment> q, CrudSearch search)
    {
        if (search is not PaymentSearch filters)
            return q;

        if (filters.CompanyId is { } companyId)
            q = q.Where(p => p.PatientRegistration.CompanyId == companyId);

        if (filters.Date is { } date)
        {
            var (from, to) = DayBoundsUtc(date);
            q = q.Where(p => p.PaymentDate >= from && p.PaymentDate < to);
        }

        return q;
    }

    protected override PaymentListItem ToListItem(Payment p) => new(
        p.Id, LocalDate(p.PaymentDate), p.PatientRegistration.RegistrationCode, p.PatientRegistration.Patient.PatientName,
        p.PatientRegistration.Company?.CompanyName, p.PaymentAmount, p.Type);

    protected override PaymentDetails ToDetails(Payment p) =>
        new(p.Id, p.PatientRegistrationId, LocalDate(p.PaymentDate), p.Type, p.PaymentAmount, p.RowVersion);

    // ---------------------------------------------------------------- validation

    protected override IReadOnlyList<string> Validate(PaymentInput input)
    {
        var errors = new List<string>();

        if (input.RegistrationId == 0)
            errors.Add("Find the registration to pay first.");

        if (input.Type == PaymentType.Payment && input.Amount <= 0)
            errors.Add("The payment amount must be more than zero.");
        if (input.Type == PaymentType.Charge && input.Amount != 0)
            errors.Add("A charge has no payment amount.");

        if (input.DiscountAmount is not null && input.DiscountPercentage is not null)
            errors.Add("Give the discount as an amount or as a percentage, not both.");
        if (input.DiscountAmount is < 0)
            errors.Add("The discount amount must not be negative.");
        if (input.DiscountPercentage is < 0 or > 100)
            errors.Add("The discount percentage must be between 0 and 100.");

        return errors;
    }

    protected override async Task<IReadOnlyList<string>> ValidateAsync(PaymentInput input, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var registration = await Db.PatientRegistrations.AsNoTracking()
            .Where(r => r.Id == input.RegistrationId)
            .Select(r => new { r.AmountDue })
            .FirstOrDefaultAsync(cancellationToken);
        if (registration is null)
        {
            errors.Add("The registration no longer exists.");
            return errors;
        }

        if (input.Id != 0)
        {
            var registrationId = await Db.Payments.Where(p => p.Id == input.Id).Select(p => (long?)p.PatientRegistrationId).FirstOrDefaultAsync(cancellationToken);
            if (registrationId is { } existing && existing != input.RegistrationId)
                errors.Add("A payment can not be moved to a different registration.");
        }

        // A maintained discount gives all its details, one after another. A registration that already has this discount keeps the
        // details it was given; a different (or newly chosen) discount is copied from the discount as it is now.
        IReadOnlyList<DiscountStep>? steps = null;
        _stepsToGive = null;
        if (input.DiscountId is { } discountId)
        {
            var current = await Db.PatientRegistrations.AsNoTracking().Where(r => r.Id == input.RegistrationId)
                .Select(r => new { r.DiscountId, Steps = r.DiscountSteps.OrderBy(s => s.Sequence).Select(s => new DiscountStep(s.Amount, s.Percentage)).ToList() })
                .FirstOrDefaultAsync(cancellationToken);
            var active = await Db.Discounts.AsNoTracking().AnyAsync(d => d.Id == discountId && (d.IsActive || current!.DiscountId == discountId), cancellationToken);
            if (!active)
            {
                errors.Add("That discount is no longer offered.");
                return errors;
            }

            if (current!.DiscountId == discountId && current.Steps.Count > 0)
            {
                steps = current.Steps;
            }
            else
            {
                steps = await Db.DiscountDetails.AsNoTracking().Where(d => d.DiscountId == discountId).OrderBy(d => d.Id)
                    .Select(d => new DiscountStep(d.Amount, d.Percentage)).ToListAsync(cancellationToken);
                _stepsToGive = steps;
            }
        }
        else if (input.DiscountAmount is { } amount && amount > registration.AmountDue)
        {
            errors.Add("The discount can not be more than the price.");
        }

        var discount = steps is not null
            ? BillingMath.DiscountTotal(registration.AmountDue, steps)
            : BillingMath.DiscountTotal(registration.AmountDue, input.DiscountAmount, input.DiscountPercentage);
        var amountDue = registration.AmountDue - discount;        var others = Db.Payments.AsNoTracking().Where(p => p.PatientRegistrationId == input.RegistrationId && p.Id != input.Id);

        if (input.Type == PaymentType.Charge)
        {
            if (await others.AnyAsync(p => p.Type == PaymentType.Charge, cancellationToken))
                errors.Add("This registration is already charged.");
        }
        else if (await others.AnyAsync(p => p.Type == PaymentType.Charge, cancellationToken))
        {
            errors.Add("This registration is charged to its company, so no payment is needed.");
        }
        else
        {
            var paid = await others.Where(p => p.Type == PaymentType.Payment).SumAsync(p => (decimal?)p.PaymentAmount, cancellationToken) ?? 0m;
            var balance = BillingMath.Round(amountDue - paid);
            if (input.Amount > balance)
            {
                errors.Add(balance <= 0
                    ? "This registration is already fully paid."
                    : $"The payment is more than the balance of {balance:N2}.");
            }
        }

        return errors;
    }

    // ---------------------------------------------------------------- saving

    protected override async Task<Payment> CreateAsync(PaymentInput input, CancellationToken cancellationToken) =>
        new() { PatientRegistration = await Db.PatientRegistrations.Include(r => r.Patient).Include(r => r.DiscountSteps).FirstAsync(r => r.Id == input.RegistrationId, cancellationToken) };

    protected override void Apply(Payment payment, PaymentInput input)
    {
        // Keep the time of day of a payment whose date was not changed; a new or moved one gets "now" on that date.
        if (payment.PaymentDate == default || LocalDate(payment.PaymentDate) != input.Date)
            payment.PaymentDate = ToUtc(input.Date, clock.UtcNow.ToLocalTime().TimeOfDay);

        payment.Type = input.Type;
        payment.PaymentAmount = input.Type == PaymentType.Charge ? 0m : BillingMath.Round(input.Amount);

        // The discount belongs to the registration (as in the old app), so it is saved in the same transaction.
        var registration = payment.PatientRegistration;
        registration.DiscountId = input.DiscountId;
        if (input.DiscountId is not null)
        {
            // A maintained discount: its details (given when it was chosen) are applied one after another.
            registration.DiscountAmount = null;
            registration.DiscountPercentage = null;
            if (_stepsToGive is { } toGive)
            {
                foreach (var old in registration.DiscountSteps.ToList())
                    Db.PatientRegistrationDiscountSteps.Remove(old);

                var sequence = 0;
                foreach (var step in toGive)
                    registration.DiscountSteps.Add(new PatientRegistrationDiscountStep { Sequence = ++sequence, Amount = step.Amount, Percentage = step.Percentage });
            }

            registration.DiscountTotal = BillingMath.DiscountTotal(registration.AmountDue, StepsOf(registration));
        }
        else
        {
            foreach (var old in registration.DiscountSteps.ToList())
                Db.PatientRegistrationDiscountSteps.Remove(old);

            registration.DiscountPercentage = input.DiscountPercentage;
            registration.DiscountAmount = input.DiscountPercentage is null ? input.DiscountAmount ?? 0m : null;
            registration.DiscountTotal = BillingMath.DiscountTotal(registration.AmountDue, registration.DiscountAmount, registration.DiscountPercentage);
        }
    }

    /// <summary>The details of a registration's maintained discount, in the order they are applied (none for a typed discount).</summary>
    private static List<DiscountStep> StepsOf(PatientRegistration r) =>
        [.. r.DiscountSteps.Where(s => !s.IsDeleted).OrderBy(s => s.Sequence).Select(s => new DiscountStep(s.Amount, s.Percentage))];

    // ---------------------------------------------------------------- helpers for the form

    public async Task<Result<RegistrationBalance>> FindRegistrationAsync(string code, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.Payments, ModuleAction.View))
            return Result<RegistrationBalance>.Failure(Errors.Forbidden);

        var text = (code ?? string.Empty).Trim();
        var id = text.Length == 0
            ? null
            : await Db.PatientRegistrations.AsNoTracking().Where(r => r.RegistrationCode == text).Select(r => (long?)r.Id).FirstOrDefaultAsync(cancellationToken);

        return id is null
            ? Result<RegistrationBalance>.Failure(new Error("Registration.NotFound", "No registration has that code."))
            : await GetRegistrationAsync(id.Value, 0, cancellationToken);
    }

    public async Task<Result<RegistrationBalance>> GetRegistrationAsync(long registrationId, long exceptPaymentId = 0, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.Payments, ModuleAction.View))
            return Result<RegistrationBalance>.Failure(Errors.Forbidden);

        var r = await Db.PatientRegistrations.AsNoTracking()
            .Include(x => x.Patient).Include(x => x.Company).Include(x => x.Services).ThenInclude(s => s.Service)
            .Include(x => x.DiscountSteps)
            .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken);
        if (r is null)
            return Result<RegistrationBalance>.Failure(new Error("Registration.NotFound", "The registration no longer exists."));

        var payments = await Db.Payments.AsNoTracking()
            .Where(p => p.PatientRegistrationId == registrationId && p.Id != exceptPaymentId)
            .Select(p => new { p.Type, p.PaymentAmount })
            .ToListAsync(cancellationToken);

        var paid = payments.Where(p => p.Type == PaymentType.Payment).Sum(p => p.PaymentAmount);
        var charged = payments.Any(p => p.Type == PaymentType.Charge);

        // The stored discount total follows the price when it is saved; recompute so it is never stale.
        var steps = StepsOf(r);
        var stepResults = BillingMath.ApplySteps(r.AmountDue, steps);
        var discount = steps.Count > 0 ? stepResults.Sum(x => x.Cut) : BillingMath.DiscountTotal(r.AmountDue, r.DiscountAmount, r.DiscountPercentage);
        var amountDue = r.AmountDue - discount;
        var balance = charged ? 0m : BillingMath.Round(amountDue - paid);

        return Result<RegistrationBalance>.Success(new RegistrationBalance(
            r.Id, r.RegistrationCode, LocalDate(r.InputDate), r.Patient.PatientCode, r.Patient.PatientName, r.Company?.CompanyName, r.BatchName,
            [.. r.Services.Where(s => !s.IsDeleted).OrderBy(s => s.Id).Select(s => new PaymentServiceLine(s.Service.ServiceName, s.Price))],
            r.AmountDue, r.DiscountAmount, r.DiscountPercentage, discount, amountDue, paid, balance, charged, r.DiscountId,
            [.. stepResults.Select(x => new DiscountStepLine(x.Step.Amount, x.Step.Percentage, x.Cut))]));
    }

    public async Task<Result<IReadOnlyList<RegistrationMatch>>> SuggestRegistrationsAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.Payments, ModuleAction.View))
            return Result<IReadOnlyList<RegistrationMatch>>.Failure(Errors.Forbidden);

        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0 || string.Concat(words).Length < MinSuggestionLength)
            return Result<IReadOnlyList<RegistrationMatch>>.Success([]);

        var query = Db.PatientRegistrations.AsNoTracking();
        foreach (var word in words)
        {
            var term = word;
            query = query.Where(r => r.RegistrationCode.Contains(term) || r.Patient.PatientName.Contains(term) || r.Patient.PatientCode.Contains(term));
        }

        var rows = await query.OrderByDescending(r => r.InputDate).ThenByDescending(r => r.Id).Take(MaxSuggestions)
            .Select(r => new
            {
                r.Id, r.RegistrationCode, r.Patient.PatientName, r.InputDate, r.AmountDue, r.DiscountAmount, r.DiscountPercentage,
                Steps = r.DiscountSteps.OrderBy(s => s.Sequence).Select(s => new { s.Amount, s.Percentage }).ToList(),
                Paid = r.Payments.Where(p => p.Type == PaymentType.Payment).Sum(p => (decimal?)p.PaymentAmount) ?? 0m,
                Charged = r.Payments.Any(p => p.Type == PaymentType.Charge),
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<RegistrationMatch> matches =
        [
            .. rows.Select(r => new RegistrationMatch(
                r.Id, r.RegistrationCode, r.PatientName, LocalDate(r.InputDate),
                r.Charged ? 0m : BillingMath.Round(r.AmountDue
                    - (r.Steps.Count > 0
                        ? BillingMath.DiscountTotal(r.AmountDue, r.Steps.Select(s => new DiscountStep(s.Amount, s.Percentage)))
                        : BillingMath.DiscountTotal(r.AmountDue, r.DiscountAmount, r.DiscountPercentage))
                    - r.Paid))),
        ];

        return Result<IReadOnlyList<RegistrationMatch>>.Success(matches);
    }

    // Dates are chosen as local calendar days but stored as UTC instants.
    private static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime());

    private static DateTime ToUtc(DateOnly date, TimeSpan timeOfDay) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).Add(timeOfDay).ToUniversalTime();

    private static (DateTime From, DateTime To) DayBoundsUtc(DateOnly date) =>
        (ToUtc(date, TimeSpan.Zero), ToUtc(date.AddDays(1), TimeSpan.Zero));
}
