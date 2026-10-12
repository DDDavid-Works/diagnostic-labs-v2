using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Application.Menu;
using DiagnosticLabs.Application.Patients;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.Payments;
using DiagnosticLabs.Application.Registrations;
using DiagnosticLabs.Application.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticLabs.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CurrentUserSession>();
        services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<CurrentUserSession>());
        services.AddSingleton<ICurrentUserSession>(sp => sp.GetRequiredService<CurrentUserSession>());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IEntryService, EntryService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ILabRegistrationLookup, LabRegistrationLookup>();
        services.AddScoped<IModuleDefaultsService, ModuleDefaultsService>();
        services.AddScoped<IStoolFecalysisService, StoolFecalysisService>();
        services.AddScoped<IUrinalysisService, UrinalysisService>();
        services.AddScoped<IHematologyService, HematologyService>();
        services.AddScoped<IPhysicalExaminationService, PhysicalExaminationService>();
        services.AddScoped<IClinicalMicroscopyService, ClinicalMicroscopyService>();
        services.AddScoped<ISerologyService, SerologyService>();
        services.AddScoped<IImmunologyService, ImmunologyService>();
        services.AddScoped<IPregnancyTestService, PregnancyTestService>();
        services.AddScoped<IClinicalChemistryService, ClinicalChemistryService>();
        services.AddScoped<IClinicalChemistry1Service, ClinicalChemistry1Service>();
        services.AddScoped<IClinicalChemistry2Service, ClinicalChemistry2Service>();
        services.AddScoped<IMedicalExaminationService, MedicalExaminationService>();
        services.AddScoped<DiagnosticLabs.Application.Board.IRegistrationBoardService, DiagnosticLabs.Application.Board.RegistrationBoardService>();
        services.AddScoped<IAnnualPhysicalExamService, AnnualPhysicalExamService>();
        services.AddScoped<IReferenceLookups, ReferenceLookups>();
        services.AddScoped<DiagnosticLabs.Application.Reports.ISalesReportService, DiagnosticLabs.Application.Reports.SalesReportService>();
        services.AddScoped<DiagnosticLabs.Application.Reports.IStatementOfAccountService, DiagnosticLabs.Application.Reports.StatementOfAccountService>();
        services.AddScoped<DiagnosticLabs.Application.LabResults.ILabResultPrinters, DiagnosticLabs.Application.LabResults.LabResultPrinters>();
        services.AddScoped<DiagnosticLabs.Application.Reports.IBatchPrintService, DiagnosticLabs.Application.Reports.BatchPrintService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IItemLocationService, ItemLocationService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICompanySetupService, CompanySetupService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IPackageCatalogService, PackageCatalogService>();
        return services;
    }
}
