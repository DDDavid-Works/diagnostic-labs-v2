using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>What Serology and Immunology results are made of: the name of the test (picked from a maintained list) and its result text.</summary>
public sealed record TestResultDetails(long Id, LabResultHeader Header, string? Test, string Result, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record TestResultInput(long Id, LabResultHeader Header, string? Test, string? Result, byte[]? RowVersion) : ICrudInput;

public interface ISerologyService : ICrudService<LabResultListItem, TestResultDetails, TestResultInput>, ILabResultPrinting;

public interface IImmunologyService : ICrudService<LabResultListItem, TestResultDetails, TestResultInput>, ILabResultPrinting;

public interface IPregnancyTestService : ICrudService<LabResultListItem, TestResultDetails, TestResultInput>, ILabResultPrinting;

/// <summary>The part Serology and Immunology share. The two differ only in their table, module and title.</summary>
public abstract class TestResultService<TDetail>(
    IAppDbContext db, ICurrentUser currentUser, IClock clock, int moduleId, string name, LabReportType type)
    : LabResultService<TestResultInput, TestResultDetails, TDetail>(db, currentUser, clock, moduleId, name, type)
    where TDetail : LabReportDetail, new()
{
    public const int TestMaxLength = 100;
    public const int ResultMaxLength = 500;

    protected abstract string? TestOf(TDetail detail);

    protected abstract string ResultOf(TDetail detail);

    protected abstract void Assign(TDetail detail, string? test, string result);

    /// <summary>Whether the form has a test to pick (Pregnancy Test has only its result).</summary>
    protected virtual bool HasTest => true;

    protected override ReportLayout Layout => ReportLayout.TestResult;

    protected override LabResultHeader HeaderOf(TestResultInput input) => input.Header;

    protected override IEnumerable<string> ValidateDetail(TestResultInput input)
    {
        var errors = new List<string>();
        if (HasTest)
            Max(errors, input.Test, TestMaxLength, "Test");

        Max(errors, input.Result, ResultMaxLength, "Result");
        return errors;
    }

    protected override void ApplyDetail(TDetail detail, TestResultInput input) =>
        Assign(detail, HasTest ? Clean(input.Test) : null, input.Result?.Trim() ?? string.Empty);

    protected override TestResultDetails BuildDetails(LabReport report, LabResultHeader header, TDetail detail) =>
        new(report.Id, header, TestOf(detail), ResultOf(detail), report.Photo?.Content, report.RowVersion);

    protected override IReadOnlyList<PrintLine> ResultLines(TDetail detail) => HasTest ? [new("Test", TestOf(detail))] : [];

    protected override IReadOnlyList<PrintText> ResultTexts(TDetail detail) => [new("Result", ResultOf(detail))];
}

public sealed class SerologyService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : TestResultService<SerologyReport>(db, currentUser, clock, ModuleIds.Serology, "Serology", LabReportType.Serology), ISerologyService
{
    protected override string Title => "Serology";

    protected override string? TestOf(SerologyReport detail) => detail.Test;

    protected override string ResultOf(SerologyReport detail) => detail.Result;

    protected override void Assign(SerologyReport detail, string? test, string result)
    {
        detail.Test = test;
        detail.Result = result;
    }
}

public sealed class ImmunologyService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : TestResultService<ImmunologyReport>(db, currentUser, clock, ModuleIds.Immunology, "Immunology", LabReportType.Immunology), IImmunologyService
{
    protected override string Title => "Immunology";

    protected override string? TestOf(ImmunologyReport detail) => detail.Test;

    protected override string ResultOf(ImmunologyReport detail) => detail.Result;

    protected override void Assign(ImmunologyReport detail, string? test, string result)
    {
        detail.Test = test;
        detail.Result = result;
    }
}

/// <summary>The pregnancy test has no test to pick: only its result and the remarks.</summary>
public sealed class PregnancyTestService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : TestResultService<PregnancyTestReport>(db, currentUser, clock, ModuleIds.PregnancyTest, "Pregnancy Test", LabReportType.PregnancyTest), IPregnancyTestService
{
    protected override string Title => "Pregnancy Test";

    protected override bool HasTest => false;

    protected override string? TestOf(PregnancyTestReport detail) => null;

    protected override string ResultOf(PregnancyTestReport detail) => detail.Result;

    protected override void Assign(PregnancyTestReport detail, string? test, string result) => detail.Result = result;
}