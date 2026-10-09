namespace DiagnosticLabs.Application.Abstractions;

/// <summary>Ids of the rows seeded in the Modules table that screens are permission-checked against.</summary>
public static class ModuleIds
{
    public const int PatientRegistrations = 1;
    public const int Patients = 2;
    public const int Companies = 3;
    public const int Payments = 4;
    public const int Departments = 18;
    public const int Services = 19;
    public const int Packages = 20;
    public const int Items = 21;
    public const int ItemLocations = 22;
    public const int Discounts = 23;
    public const int CompanySetup = 26;
    public const int Users = 27;

    /// <summary>Change Password is available to every signed-in user from the header, so it is not a permission module.</summary>
    public const int ChangePassword = 28;
}
