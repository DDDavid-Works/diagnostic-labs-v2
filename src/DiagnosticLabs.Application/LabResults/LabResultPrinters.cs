using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults.CustomForms;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>Finds the service that prints the saved results of a result module (for printing many results without opening their screens).</summary>
public interface ILabResultPrinters
{
    /// <summary>The printer of a result module, or <c>null</c> for a module that has no result form.</summary>
    ILabResultPrinting? For(int moduleId);
}

public sealed class LabResultPrinters(IServiceProvider services) : ILabResultPrinters
{
    public ILabResultPrinting? For(int moduleId) => moduleId switch
    {
        ModuleIds.StoolFecalysis => services.GetRequiredService<IStoolFecalysisService>(),
        ModuleIds.Urinalysis => services.GetRequiredService<IUrinalysisService>(),
        ModuleIds.Hematology => services.GetRequiredService<IHematologyService>(),
        ModuleIds.Immunology => services.GetRequiredService<IImmunologyService>(),
        ModuleIds.Serology => services.GetRequiredService<ISerologyService>(),
        ModuleIds.PregnancyTest => services.GetRequiredService<IPregnancyTestService>(),
        ModuleIds.ClinicalChemistry => services.GetRequiredService<IClinicalChemistryService>(),
        ModuleIds.ClinicalChemistry1 => services.GetRequiredService<IClinicalChemistry1Service>(),
        ModuleIds.ClinicalChemistry2 => services.GetRequiredService<IClinicalChemistry2Service>(),
        ModuleIds.AnnualPhysicalExam => services.GetRequiredService<IAnnualPhysicalExamService>(),
        ModuleIds.AnnualPhysicalExamPage2 => services.GetRequiredService<IMedicalExaminationService>(),
        ModuleIds.PhysicalExamination => services.GetRequiredService<IPhysicalExaminationService>(),
        ModuleIds.ClinicalMicroscopy => services.GetRequiredService<IClinicalMicroscopyService>(),
        // a form made in the Custom Lab Form designer: its own service instance, told which form it is for
        >= ModuleIds.FirstCustomForm => ActivatorUtilities.CreateInstance<CustomFormResultService>(services, new CustomFormContext { ModuleId = moduleId }),
        _ => null,
    };
}
