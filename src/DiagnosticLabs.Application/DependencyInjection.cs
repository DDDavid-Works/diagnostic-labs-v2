using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Application.Menu;
using DiagnosticLabs.Application.Patients;
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
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IReferenceLookups, ReferenceLookups>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IItemLocationService, ItemLocationService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IPackageCatalogService, PackageCatalogService>();
        return services;
    }
}
