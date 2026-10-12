using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Billing;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Reports;

/// <summary>
/// What a Statement of Account is made for. Company mode: one company (<see cref="CompanyId"/>), optionally one batch of it, with the registrations
/// that were charged to the company. Patient mode: one patient (<see cref="PatientId"/>) and everything they were billed. Both may be limited to a range
/// of registration days (either end may be left out).
/// </summary>
public sealed record StatementRequest(
    StatementMode Mode,
    long? CompanyId = null,
    string? Batch = null,
    long? PatientId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Attention = null);

/// <summary>One registration on a statement: what it came to, the discount, and (patient mode) what was paid and what is left.</summary>
public sealed record StatementLine(
    long RegistrationId, string RegistrationCode, string PatientName, DateOnly Date, decimal Gross, decimal Discount, decimal Net, decimal Paid, decimal Balance);

/// <summary>
/// A statement as it is printed. <see cref="Id"/> is 0 and <see cref="Number"/> empty for a preview that has not been saved. The totals are the sums of the
/// lines, which is also what was stored when the statement was generated.
/// </summary>
public sealed record StatementData(
    long Id,
    string Number,
    ReportLetterhead Letterhead,
    StatementMode Mode,
    string? CompanyName,
    string? Batch,
    string? Attention,
    string? PatientName,
    string? PatientCode,
    DateOnly? From,
    DateOnly? To,
    DateTime GeneratedAtUtc,
    bool IsVoid,
    IReadOnlyList<StatementLine> Lines)
{
    /// <summary>The company or patient the statement is for (kept so it can be saved and regenerated).</summary>
    public long? CompanyId { get; init; }

    public long? PatientId { get; init; }

    public decimal GrossTotal => Lines.Sum(l => l.Gross);

    public decimal DiscountTotal => Lines.Sum(l => l.Discount);

    public decimal NetTotal => Lines.Sum(l => l.Net);

    public decimal PaidTotal => Lines.Sum(l => l.Paid);

    public decimal BalanceTotal => Lines.Sum(l => l.Balance);
}

/// <summary>A saved statement in the list: enough to find it and see what it came to.</summary>
public sealed record StatementListItem(
    long Id,
    string Number,
    StatementMode Mode,
    string Name,
    string? Batch,
    DateOnly? From,
    DateOnly? To,
    DateOnly GeneratedOn,
    decimal Net,
    decimal Balance,
    bool IsVoid,
    int LineCount);

/// <summary>Which saved statements to list: matching text (number, company, batch or patient), the mode, the days they were generated, and whether voided ones are shown.</summary>
public sealed record StatementSearch(
    string? Text = null,
    StatementMode? Mode = null,
    DateOnly? GeneratedFrom = null,
    DateOnly? GeneratedTo = null,
    bool IncludeVoid = true);

public sealed record StatementPatientSuggestion(long Id, string PatientCode, string PatientName);

public interface IStatementOfAccountService
{
    /// <summary>The statement the request would make now, without saving it (the Generate screen shows it before it is saved).</summary>
    Task<Result<StatementData>> PreviewAsync(StatementRequest request, CancellationToken cancellationToken = default);

    /// <summary>Makes the statement and saves it with the next statement number.</summary>
    Task<Result<StatementData>> GenerateAsync(StatementRequest request, CancellationToken cancellationToken = default);

    /// <summary>A saved statement exactly as it was generated (for reprinting).</summary>
    Task<Result<StatementData>> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatementListItem>> SearchAsync(StatementSearch search, CancellationToken cancellationToken = default);

    /// <summary>Voids a statement: it stays in the list (and prints marked VOID) but no longer counts.</summary>
    Task<Result> VoidAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Makes a new statement with the same parameters from the registrations as they are now, and voids the old one.</summary>
    Task<Result<StatementData>> RegenerateAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatementPatientSuggestion>> SuggestPatientsAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>The batches of a company that have registrations charged to it (what a company statement can cover).</summary>
    Task<IReadOnlyList<string>> GetChargedBatchesAsync(long companyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statements of Account. Company mode bills a company for the registrations charged to it (no payments are shown, the company pays the statement);
/// patient mode shows each registration with what the patient paid and what is left. A statement is saved with a copy of its lines so it can be
/// reprinted unchanged; it is never edited, only voided and generated again.
/// </summary>
public sealed class StatementOfAccountService(IAppDbContext db, ICurrentUser currentUser, ICodeGenerator codes, IClock clock) : IStatementOfAccountService
{
    public const int MaxSuggestions = 8;
    public const int MinSuggestionLength = 2;

    // ---------------------------------------------------------------- making a statement

    public async Task<Result<StatementData>> PreviewAsync(StatementRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Create))
            return Result<StatementData>.Failure(Errors.Forbidden);

        return await BuildAsync(request, cancellationToken);
    }

    public async Task<Result<StatementData>> GenerateAsync(StatementRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Create))
            return Result<StatementData>.Failure(Errors.Forbidden);

        var built = await BuildAsync(request, cancellationToken);
        if (built.IsFailure)
            return built;

        var entity = await SaveAsync(built.Value, regeneratedFromId: null, voidOld: null, cancellationToken);
        return Result<StatementData>.Success(ToData(entity, built.Value.Letterhead));
    }

    public async Task<Result<StatementData>> RegenerateAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Create) || !currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Delete))
            return Result<StatementData>.Failure(Errors.Forbidden);

        var old = await db.StatementsOfAccount.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (old is null)
            return Result<StatementData>.Failure(Errors.NotFound("Statement"));

        var request = new StatementRequest(old.Mode, old.CompanyId, old.BatchName, old.PatientId, old.DateFrom, old.DateTo, old.Attention);
        var built = await BuildAsync(request, cancellationToken);
        if (built.IsFailure)
            return built;

        var entity = await SaveAsync(built.Value, old.Id, old.IsVoid ? null : old, cancellationToken);
        return Result<StatementData>.Success(ToData(entity, built.Value.Letterhead));
    }

    private async Task<StatementOfAccount> SaveAsync(StatementData data, long? regeneratedFromId, StatementOfAccount? voidOld, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var entity = new StatementOfAccount
        {
            StatementNumber = await codes.NextStatementNumberAsync(LocalTime.ToLocalDate(now), cancellationToken),
            Mode = data.Mode,
            CompanyId = data.Mode == StatementMode.Company ? data.CompanyId : null,
            CompanyName = data.CompanyName,
            BatchName = data.Batch,
            Attention = data.Attention,
            PatientId = data.PatientId,
            PatientName = data.PatientName,
            PatientCode = data.PatientCode,
            DateFrom = data.From,
            DateTo = data.To,
            GrossTotal = data.GrossTotal,
            DiscountTotal = data.DiscountTotal,
            NetTotal = data.NetTotal,
            PaidTotal = data.PaidTotal,
            BalanceTotal = data.BalanceTotal,
            RegeneratedFromId = regeneratedFromId,
        };

        var sequence = 0;
        foreach (var line in data.Lines)
        {
            entity.Lines.Add(new StatementOfAccountLine
            {
                Sequence = ++sequence,
                PatientRegistrationId = line.RegistrationId,
                RegistrationCode = line.RegistrationCode,
                PatientName = line.PatientName,
                RegistrationDate = line.Date,
                Gross = line.Gross,
                Discount = line.Discount,
                Net = line.Net,
                Paid = line.Paid,
                Balance = line.Balance,
            });
        }

        db.StatementsOfAccount.Add(entity);
        if (voidOld is not null)
            MarkVoid(voidOld, now);

        // One save: the new statement and the voiding of the old one happen together.
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private void MarkVoid(StatementOfAccount statement, DateTime now)
    {
        statement.IsVoid = true;
        statement.VoidedAtUtc = now;
        statement.VoidedByUserId = currentUser.UserId;
    }

    public async Task<Result> VoidAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Delete))
            return Result.Failure(Errors.Forbidden);

        var statement = await db.StatementsOfAccount.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (statement is null)
            return Result.Failure(Errors.NotFound("Statement"));
        if (statement.IsVoid)
            return Result.Failure(Errors.Invalid(["This statement is already void."]));

        MarkVoid(statement, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ---------------------------------------------------------------- the saved ones

    public async Task<Result<StatementData>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Print))
            return Result<StatementData>.Failure(Errors.Forbidden);

        var entity = await db.StatementsOfAccount.AsNoTracking().Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity is null)
            return Result<StatementData>.Failure(Errors.NotFound("Statement"));

        return Result<StatementData>.Success(ToData(entity, await LetterheadAsync(cancellationToken)));
    }

    public async Task<IReadOnlyList<StatementListItem>> SearchAsync(StatementSearch search, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.View))
            return [];

        var query = db.StatementsOfAccount.AsNoTracking().AsQueryable();
        if (search.Mode is { } mode)
            query = query.Where(s => s.Mode == mode);
        if (!search.IncludeVoid)
            query = query.Where(s => !s.IsVoid);
        if (search.GeneratedFrom is { } from)
        {
            var start = LocalTime.DayBoundsUtc(from).From;
            query = query.Where(s => s.CreatedAtUtc >= start);
        }

        if (search.GeneratedTo is { } to)
        {
            var end = LocalTime.DayBoundsUtc(to).To;
            query = query.Where(s => s.CreatedAtUtc < end);
        }

        foreach (var word in (search.Text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var term = word;
            query = query.Where(s => s.StatementNumber.Contains(term)
                                     || (s.CompanyName != null && s.CompanyName.Contains(term))
                                     || (s.BatchName != null && s.BatchName.Contains(term))
                                     || (s.PatientName != null && s.PatientName.Contains(term))
                                     || (s.PatientCode != null && s.PatientCode.Contains(term)));
        }

        var rows = await query.OrderByDescending(s => s.CreatedAtUtc).ThenByDescending(s => s.Id).Take(500)
            .Select(s => new
            {
                s.Id,
                s.StatementNumber,
                s.Mode,
                s.CompanyName,
                s.PatientName,
                s.BatchName,
                s.DateFrom,
                s.DateTo,
                s.CreatedAtUtc,
                s.NetTotal,
                s.BalanceTotal,
                s.IsVoid,
                LineCount = s.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(r => new StatementListItem(
                r.Id,
                r.StatementNumber,
                r.Mode,
                (r.Mode == StatementMode.Company ? r.CompanyName : r.PatientName) ?? string.Empty,
                r.BatchName,
                r.DateFrom,
                r.DateTo,
                LocalTime.ToLocalDate(r.CreatedAtUtc),
                r.NetTotal,
                r.BalanceTotal,
                r.IsVoid,
                r.LineCount)),
        ];
    }

    // ---------------------------------------------------------------- finding what goes on it

    private async Task<Result<StatementData>> BuildAsync(StatementRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (request.From is { } from && request.To is { } to && to < from)
            errors.Add("The \"to\" date is before the \"from\" date.");

        string? companyName = null, patientName = null, patientCode = null;
        long? companyId = null, patientId = null;
        var batch = request.Batch?.Trim();
        if (request.Mode == StatementMode.Company)
        {
            if (request.CompanyId is null or 0)
                errors.Add("Pick the company.");
            else
            {
                companyId = request.CompanyId;
                companyName = await db.Companies.AsNoTracking().Where(c => c.Id == companyId).Select(c => c.CompanyName).FirstOrDefaultAsync(cancellationToken);
                if (companyName is null)
                    errors.Add("The company no longer exists.");
            }
        }
        else
        {
            if (request.PatientId is null or 0)
                errors.Add("Pick the patient.");
            else
            {
                patientId = request.PatientId;
                var patient = await db.Patients.AsNoTracking().Where(p => p.Id == patientId).Select(p => new { p.PatientName, p.PatientCode }).FirstOrDefaultAsync(cancellationToken);
                if (patient is null)
                    errors.Add("The patient no longer exists.");
                else
                {
                    patientName = patient.PatientName;
                    patientCode = patient.PatientCode;
                }
            }
        }

        if (errors.Count > 0)
            return Result<StatementData>.Failure(Errors.Invalid(errors));

        var query = db.PatientRegistrations.AsNoTracking().Where(r => r.RegistrationCode != RegistrationBoardService.DefaultRegistrationCode);
        if (request.From is { } start)
        {
            var startUtc = LocalTime.DayBoundsUtc(start).From;
            query = query.Where(r => r.InputDate >= startUtc);
        }

        if (request.To is { } end)
        {
            var endUtc = LocalTime.DayBoundsUtc(end).To;
            query = query.Where(r => r.InputDate < endUtc);
        }

        if (request.Mode == StatementMode.Company)
        {
            // Only what was charged to the company: the company pays these.
            query = query.Where(r => r.CompanyId == companyId && r.Payments.Any(p => p.Type == PaymentType.Charge));
            if (!string.IsNullOrEmpty(batch))
                query = query.Where(r => r.BatchName == batch);
        }
        else
        {
            query = query.Where(r => r.PatientId == patientId);
        }

        var registrations = await query
            .Select(r => new
            {
                r.Id,
                r.RegistrationCode,
                r.Patient.PatientName,
                r.InputDate,
                r.AmountDue,
                r.DiscountTotal,
                Charged = r.Payments.Any(p => p.Type == PaymentType.Charge),
                Paid = r.Payments.Where(p => p.Type == PaymentType.Payment).Sum(p => (decimal?)p.PaymentAmount) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        if (registrations.Count == 0)
        {
            return Result<StatementData>.Failure(Errors.Invalid(
                [request.Mode == StatementMode.Company
                    ? "No registration charged to that company matches. Nothing to put on a statement."
                    : "That patient has no registration in that period. Nothing to put on a statement."]));
        }

        // Company statements read by patient name, patient statements by date.
        var ordered = request.Mode == StatementMode.Company
            ? registrations.OrderBy(r => r.PatientName, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.InputDate).ThenBy(r => r.Id)
            : registrations.OrderBy(r => r.InputDate).ThenBy(r => r.Id);

        var lines = ordered.Select(r =>
        {
            var net = BillingMathRound(r.AmountDue - r.DiscountTotal);
            if (request.Mode == StatementMode.Company)
                return new StatementLine(r.Id, r.RegistrationCode, r.PatientName, LocalTime.ToLocalDate(r.InputDate), r.AmountDue, r.DiscountTotal, net, 0m, 0m);

            // A registration charged to a company leaves nothing for the patient to settle.
            var balance = r.Charged ? 0m : Math.Max(BillingMathRound(net - r.Paid), 0m);
            return new StatementLine(r.Id, r.RegistrationCode, r.PatientName, LocalTime.ToLocalDate(r.InputDate), r.AmountDue, r.DiscountTotal, net, r.Paid, balance);
        }).ToList();

        return Result<StatementData>.Success(new StatementData(
            0,
            string.Empty,
            await LetterheadAsync(cancellationToken),
            request.Mode,
            companyName,
            string.IsNullOrEmpty(batch) || request.Mode != StatementMode.Company ? null : batch,
            request.Mode == StatementMode.Company && !string.IsNullOrWhiteSpace(request.Attention) ? request.Attention.Trim() : null,
            patientName,
            patientCode,
            request.From,
            request.To,
            clock.UtcNow,
            false,
            lines)
        {
            CompanyId = companyId,
            PatientId = patientId,
        });
    }

    private static decimal BillingMathRound(decimal value) => BillingMath.Round(value);

    private async Task<ReportLetterhead> LetterheadAsync(CancellationToken cancellationToken)
    {
        var setup = await db.CompanySetups.AsNoTracking().OrderByDescending(c => c.UpdatedAtUtc).ThenByDescending(c => c.Id).FirstOrDefaultAsync(cancellationToken);
        return new ReportLetterhead(setup?.CompanyName ?? string.Empty, setup?.SubCompanyName, setup?.Address, setup?.ContactNumbers, setup?.Email, setup?.Logo);
    }

    private static StatementData ToData(StatementOfAccount s, ReportLetterhead letterhead) => new(
        s.Id,
        s.StatementNumber,
        letterhead,
        s.Mode,
        s.CompanyName,
        s.BatchName,
        s.Attention,
        s.PatientName,
        s.PatientCode,
        s.DateFrom,
        s.DateTo,
        s.CreatedAtUtc,
        s.IsVoid,
        [
            .. s.Lines.OrderBy(l => l.Sequence).Select(l => new StatementLine(
                l.PatientRegistrationId, l.RegistrationCode, l.PatientName, l.RegistrationDate, l.Gross, l.Discount, l.Net, l.Paid, l.Balance)),
        ])
    {
        CompanyId = s.CompanyId,
        PatientId = s.PatientId,
    };

    // ---------------------------------------------------------------- pickers

    public async Task<IReadOnlyList<StatementPatientSuggestion>> SuggestPatientsAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Create))
            return [];

        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0 || string.Concat(words).Length < MinSuggestionLength)
            return [];

        var query = db.Patients.AsNoTracking().AsQueryable();
        foreach (var word in words)
        {
            var term = word;
            query = query.Where(p => p.PatientName.Contains(term) || p.PatientCode.Contains(term));
        }

        return await query.OrderBy(p => p.PatientName).ThenBy(p => p.Id).Take(MaxSuggestions)
            .Select(p => new StatementPatientSuggestion(p.Id, p.PatientCode, p.PatientName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetChargedBatchesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.StatementOfAccounts, ModuleAction.Create))
            return [];

        return await db.PatientRegistrations.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.BatchName != string.Empty && r.Payments.Any(p => p.Type == PaymentType.Charge))
            .Select(r => r.BatchName)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(cancellationToken);
    }
}
