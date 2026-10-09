namespace DiagnosticLabs.Application.Abstractions;

/// <summary>Ids of the rows seeded in the Modules table that screens are permission-checked against.</summary>
public static class ModuleIds
{
    public const int PatientRegistrations = 1;
    public const int Patients = 2;
    public const int Companies = 3;
    public const int Payments = 4;
    public const int StoolFecalysis = 5;
    public const int Urinalysis = 6;
    public const int Hematology = 7;
    public const int Immunology = 8;
    public const int Serology = 9;
    public const int PregnancyTest = 10;
    public const int ClinicalChemistry = 11;
    public const int ClinicalChemistry1 = 12;
    public const int ClinicalChemistry2 = 13;
    public const int MedicalExamination = 14;
    public const int MedicalExaminationPage2 = 15;
    public const int AnnualPhysicalExam = 16;
    public const int AnnualPhysicalExamPage2 = 17;
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
