using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.LabResults.CustomForms;

public sealed record FormSummary(long Id, string Name, int ModuleId, string? ServiceName, bool IsActive, int VersionNumber);

public sealed record ServiceChoice(long Id, string Name);

public sealed record FormDetails(long Id, string Name, int ModuleId, long? ServiceId, bool IsActive, int VersionNumber, FormDefinition Definition);

/// <summary>
/// What the designer submits. <paramref name="Id"/> is 0 for a new form. <paramref name="BaseVersionNumber"/> is the version the designer opened (0 for a new
/// form): when someone saved a newer design meanwhile, saving is refused instead of overwriting it.
/// </summary>
public sealed record FormInput(long Id, string Name, long ServiceId, FormDefinition Definition, int BaseVersionNumber);

public interface IFormDesignerService
{
    Task<Result<IReadOnlyList<FormSummary>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<FormDetails>> GetAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>The services a form can be linked to: active ones that no form or module uses yet, and the form's own (when editing).</summary>
    Task<Result<IReadOnlyList<ServiceChoice>>> GetServiceChoicesAsync(long? formId, CancellationToken cancellationToken = default);

    /// <summary>Creates a form (with its module and first version), or saves a changed design as the next version.</summary>
    Task<Result<FormDetails>> SaveAsync(FormInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hides a form (menu, Home chip) and frees its service; its saved results stay printable. Switching it on again needs a free service.
    /// A form is never deleted.
    /// </summary>
    Task<Result<FormDetails>> SetActiveAsync(long id, bool active, long? serviceId, CancellationToken cancellationToken = default);
}

public sealed class FormDesignerService(IAppDbContext db, ICurrentUser currentUser) : IFormDesignerService
{
    public const int NameMaxLength = 100;

    public async Task<Result<IReadOnlyList<FormSummary>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.FormDesigner, ModuleAction.View))
            return Result<IReadOnlyList<FormSummary>>.Failure(Errors.Forbidden);

        var rows = await db.CustomForms
            .AsNoTracking()
            .OrderByDescending(f => f.IsActive).ThenBy(f => f.Name)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.ModuleId,
                f.IsActive,
                ServiceId = f.Module.ServiceId,
                Version = f.Versions.Max(v => v.VersionNumber),
            })
            .ToListAsync(cancellationToken);

        var serviceNames = await db.Services.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.ServiceName, cancellationToken);
        IReadOnlyList<FormSummary> list =
        [
            .. rows.Select(r => new FormSummary(r.Id, r.Name, r.ModuleId, r.ServiceId is { } id && serviceNames.TryGetValue(id, out var name) ? name : null, r.IsActive, r.Version)),
        ];
        return Result<IReadOnlyList<FormSummary>>.Success(list);
    }

    public async Task<Result<FormDetails>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.FormDesigner, ModuleAction.View))
            return Result<FormDetails>.Failure(Errors.Forbidden);

        var form = await db.CustomForms.AsNoTracking().Include(f => f.Module).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (form is null)
            return Result<FormDetails>.Failure(Errors.NotFound("Form"));

        return Result<FormDetails>.Success(await ToDetailsAsync(form, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<ServiceChoice>>> GetServiceChoicesAsync(long? formId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.FormDesigner, ModuleAction.View))
            return Result<IReadOnlyList<ServiceChoice>>.Failure(Errors.Forbidden);

        var own = formId is { } id
            ? await db.CustomForms.Where(f => f.Id == id).Select(f => f.Module.ServiceId).FirstOrDefaultAsync(cancellationToken)
            : null;

        var taken = db.Modules.Where(m => m.ServiceId != null).Select(m => m.ServiceId!.Value);
        IReadOnlyList<ServiceChoice> choices =
        [
            .. (await db.Services.AsNoTracking()
                .Where(s => s.IsActive && (!taken.Contains(s.Id) || s.Id == own))
                .OrderBy(s => s.ServiceName)
                .Select(s => new { s.Id, s.ServiceName })
                .ToListAsync(cancellationToken)).Select(s => new ServiceChoice(s.Id, s.ServiceName)),
        ];
        return Result<IReadOnlyList<ServiceChoice>>.Success(choices);
    }

    public async Task<Result<FormDetails>> SaveAsync(FormInput input, CancellationToken cancellationToken = default)
    {
        var isNew = input.Id == 0;
        if (!currentUser.Can(ModuleIds.FormDesigner, isNew ? ModuleAction.Create : ModuleAction.Edit))
            return Result<FormDetails>.Failure(Errors.Forbidden);

        var name = input.Name?.Trim() ?? string.Empty;
        var errors = new List<string>();
        if (name.Length == 0)
            errors.Add("The form needs a name.");
        else if (name.Length > NameMaxLength)
            errors.Add($"The form name can not be longer than {NameMaxLength} characters.");

        if (input.Definition is null)
            errors.Add("The form has no design.");
        else
            errors.AddRange(input.Definition.Validate());

        if (errors.Count > 0)
            return Result<FormDetails>.Failure(Errors.Invalid(errors));

        if (await db.CustomForms.AnyAsync(f => f.Id != input.Id && f.Name == name, cancellationToken))
            return Result<FormDetails>.Failure(Errors.Invalid([$"There is already a form named '{name}'."]));

        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == input.ServiceId && s.IsActive, cancellationToken);
        if (service is null)
            return Result<FormDetails>.Failure(Errors.Invalid(["Choose the service this form belongs to."]));

        CustomForm form;
        if (isNew)
        {
            if (await ServiceIsTakenAsync(service.Id, null, cancellationToken))
                return Result<FormDetails>.Failure(Errors.Invalid(["That service is already used by another form."]));

            var created = await CreateAsync(name, service.Id, cancellationToken);
            if (created.IsFailure)
                return Result<FormDetails>.Failure(created.Error);

            form = created.Value;
        }
        else
        {
            var found = await db.CustomForms.Include(f => f.Module).Include(f => f.Versions).FirstOrDefaultAsync(f => f.Id == input.Id, cancellationToken);
            if (found is null)
                return Result<FormDetails>.Failure(Errors.NotFound("Form"));

            if (!found.IsActive)
                return Result<FormDetails>.Failure(Errors.Invalid(["This form is switched off. Switch it on before changing it."]));

            if (found.Versions.Max(v => v.VersionNumber) != input.BaseVersionNumber)
                return Result<FormDetails>.Failure(Errors.Conflict("Form"));

            if (await ServiceIsTakenAsync(service.Id, found.ModuleId, cancellationToken))
                return Result<FormDetails>.Failure(Errors.Invalid(["That service is already used by another form."]));

            found.Name = name;
            found.Module.ModuleName = name;
            found.Module.ServiceId = service.Id;
            form = found;
        }

        var json = input.Definition!.Cleaned().ToJson();
        var latest = form.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (latest is null || latest.DefinitionJson != json)
            form.Versions.Add(new CustomFormVersion { VersionNumber = (latest?.VersionNumber ?? 0) + 1, DefinitionJson = json });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!isNew)
        {
            // Two people saved the next version at the same time: the unique (form, version) index lets only one through.
            return Result<FormDetails>.Failure(Errors.Conflict("Form"));
        }

        return Result<FormDetails>.Success(await ToDetailsAsync(form, cancellationToken));
    }

    public async Task<Result<FormDetails>> SetActiveAsync(long id, bool active, long? serviceId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.FormDesigner, ModuleAction.Edit))
            return Result<FormDetails>.Failure(Errors.Forbidden);

        var form = await db.CustomForms.Include(f => f.Module).Include(f => f.Versions).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (form is null)
            return Result<FormDetails>.Failure(Errors.NotFound("Form"));

        if (form.IsActive != active)
        {
            if (active)
            {
                var service = serviceId is { } sid ? await db.Services.FirstOrDefaultAsync(s => s.Id == sid && s.IsActive, cancellationToken) : null;
                if (service is null)
                    return Result<FormDetails>.Failure(Errors.Invalid(["Choose the service this form belongs to."]));

                if (await ServiceIsTakenAsync(service.Id, form.ModuleId, cancellationToken))
                    return Result<FormDetails>.Failure(Errors.Invalid(["That service is already used by another form."]));

                form.Module.ServiceId = service.Id;
            }
            else
            {
                form.Module.ServiceId = null;
            }

            form.IsActive = active;
            form.Module.IsActive = active;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<FormDetails>.Success(await ToDetailsAsync(form, cancellationToken));
    }

    private Task<bool> ServiceIsTakenAsync(long serviceId, int? ownModuleId, CancellationToken cancellationToken) =>
        db.Modules.AnyAsync(m => m.ServiceId == serviceId && m.Id != ownModuleId, cancellationToken);

    /// <summary>A new form is a module of its own (so it gets a menu entry and permissions), copied from a built-in result module.</summary>
    private async Task<Result<CustomForm>> CreateAsync(string name, long serviceId, CancellationToken cancellationToken)
    {
        var template = await db.Modules.AsNoTracking()
            .Where(m => m.Id == ModuleIds.ClinicalMicroscopy || m.Id == ModuleIds.Urinalysis)
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null)
            return Result<CustomForm>.Failure(Errors.Invalid(["The Lab Results modules are missing from this database."]));

        var lastId = await db.Modules.Where(m => m.Id >= ModuleIds.FirstCustomForm).Select(m => (int?)m.Id).MaxAsync(cancellationToken);
        var moduleId = (lastId ?? ModuleIds.FirstCustomForm - 1) + 1;

        var module = new Module
        {
            Id = moduleId,
            ModuleTypeId = template.ModuleTypeId,
            ModuleName = name,
            HasView = true,
            HasCreate = true,
            HasEdit = true,
            HasDelete = true,
            HasSearch = true,
            HasPrint = true,
            HasShowList = false,
            HasSetDefaults = true,
            Icon = template.Icon,
            SortOrder = 20 + (moduleId - ModuleIds.FirstCustomForm),
            IsActive = true,
            ServiceId = serviceId,
        };
        var form = new CustomForm { Name = name, Module = module, ModuleId = moduleId, IsActive = true };
        db.Modules.Add(module);
        db.CustomForms.Add(form);
        return Result<CustomForm>.Success(form);
    }

    private async Task<FormDetails> ToDetailsAsync(CustomForm form, CancellationToken cancellationToken)
    {
        var latest = form.Versions.Count > 0
            ? form.Versions.OrderByDescending(v => v.VersionNumber).First()
            : await db.CustomFormVersions.AsNoTracking().Where(v => v.CustomFormId == form.Id).OrderByDescending(v => v.VersionNumber).FirstAsync(cancellationToken);

        return new FormDetails(form.Id, form.Name, form.ModuleId, form.Module.ServiceId, form.IsActive, latest.VersionNumber, FormDefinition.FromJson(latest.DefinitionJson));
    }
}
