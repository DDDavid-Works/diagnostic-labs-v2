using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Board;

/// <summary>A form a service's result can be entered in: a whole form, or one <see cref="Section"/> of the Physical Examination sheet.</summary>
public sealed record ServiceForm(int ModuleId, FormSection? Section);

/// <summary>
/// Which forms each service's result goes in. A service has its own form when a module points at it (<c>Modules.ServiceId</c>, the lowest module
/// when several do), and may also be entered in a section of the Physical Examination sheet (<c>ServiceFormSections</c>). Own form first, then sections.
/// Only the Physical Examination sheet has sections.
/// </summary>
public static class ResultFormLinks
{
    public static async Task<Dictionary<long, List<ServiceForm>>> LoadAsync(IAppDbContext db, bool activeOnly, CancellationToken cancellationToken)
    {
        var own = await db.Modules.AsNoTracking().Where(m => m.ServiceId != null && (!activeOnly || m.IsActive))
            .Select(m => new { m.Id, ServiceId = m.ServiceId!.Value })
            .ToListAsync(cancellationToken);
        var sections = await db.ServiceFormSections.AsNoTracking()
            .Where(s => !activeOnly || db.Modules.Any(m => m.Id == s.ModuleId && m.IsActive))
            .Select(s => new { s.ServiceId, s.ModuleId, s.Section })
            .ToListAsync(cancellationToken);

        var forms = own.GroupBy(o => o.ServiceId).ToDictionary(g => g.Key, g => new List<ServiceForm> { new(g.Min(o => o.Id), null) });
        foreach (var s in sections.Where(s => s.ModuleId == ModuleIds.PhysicalExamination).OrderBy(s => s.ModuleId))
        {
            if (!forms.TryGetValue(s.ServiceId, out var list))
                forms[s.ServiceId] = list = [];
            if (list.All(f => f.ModuleId != s.ModuleId))
                list.Add(new ServiceForm(s.ModuleId, s.Section));
        }

        return forms;
    }

    /// <summary>The report type of a form's results (null for a form that is not built).</summary>
    public static LabReportType? TypeOf(int moduleId) =>
        LabResultModules.IsCustomForm(moduleId) ? LabReportType.CustomForm : LabResultModules.ReportTypeOf(moduleId);

    /// <summary>The registrations that have the result of a form (for a section: a sheet with that section entered). Null for a form that is not built.</summary>
    public static IQueryable<long?>? Done(IAppDbContext db, ServiceForm form)
    {
        if (form.Section is { } section)
            return db.Set<PhysicalExaminationReport>().Where(PhysicalExaminationFields.Filled(section)).Select(d => d.LabReport.PatientRegistrationId);

        if (TypeOf(form.ModuleId) is not { } type)
            return null;

        var moduleId = form.ModuleId;
        var custom = LabResultModules.IsCustomForm(moduleId);
        return db.LabReports.Where(l => l.ReportType == type
                && (!custom || db.CustomFormReports.Any(c => c.LabReportId == l.Id && c.CustomFormVersion.CustomForm.ModuleId == moduleId)))
            .Select(l => l.PatientRegistrationId);
    }

    /// <summary>
    /// For a result about to be saved in a service's own form: a warning when that service's result is already entered on the registration's
    /// Physical Examination sheet; null when it is not.
    /// </summary>
    public static async Task<string?> EnteredOnSheetAsync(IAppDbContext db, int moduleId, long registrationId, CancellationToken cancellationToken)
    {
        var sections = await db.Modules.AsNoTracking().Where(m => m.Id == moduleId && m.ServiceId != null)
            .Join(db.ServiceFormSections.AsNoTracking(), m => m.ServiceId, s => (long?)s.ServiceId, (m, s) => s.Section)
            .ToListAsync(cancellationToken);

        foreach (var section in sections)
        {
            var sheets = db.Set<PhysicalExaminationReport>().AsNoTracking().Where(PhysicalExaminationFields.Filled(section));
            if (await sheets.AnyAsync(d => d.LabReport.PatientRegistrationId == registrationId, cancellationToken))
                return $"The {PhysicalExaminationFields.SectionName(section)} of this registration is already entered on the Physical Examination sheet. Save it here as well?";
        }

        return null;
    }

    /// <summary>
    /// For a Physical Examination sheet about to be saved: a warning when a section entered on it (and empty before) already has a result in that
    /// service's own form; null when none has.
    /// </summary>
    public static async Task<string?> EnteredInOwnFormAsync(
        IAppDbContext db, long registrationId, IEnumerable<FormSection> newlyEntered, CancellationToken cancellationToken)
    {
        var wanted = newlyEntered.ToList();
        if (wanted.Count == 0)
            return null;

        var links = await db.ServiceFormSections.AsNoTracking().Where(s => s.ModuleId == ModuleIds.PhysicalExamination)
            .Join(db.Modules.AsNoTracking(), s => (long?)s.ServiceId, m => m.ServiceId, (s, m) => new { s.Section, OwnModuleId = m.Id, m.ModuleName })
            .ToListAsync(cancellationToken);

        foreach (var link in links.Where(l => wanted.Contains(l.Section)))
        {
            if (Done(db, new ServiceForm(link.OwnModuleId, null)) is { } done && await done.AnyAsync(id => id == registrationId, cancellationToken))
                return $"This registration already has a {link.ModuleName} result on its own form. Enter the {PhysicalExaminationFields.SectionName(link.Section)} on this sheet as well?";
        }

        return null;
    }
}
