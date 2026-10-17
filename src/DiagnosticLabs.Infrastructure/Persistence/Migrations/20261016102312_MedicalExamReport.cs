using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MedicalExamReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicalExamReports",
                columns: table => new
                {
                    LabReportId = table.Column<long>(type: "bigint", nullable: false),
                    CivilStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HxEyeDisease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxEarInfection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHearingDefect = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxSinusitis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxNoseBleeding = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxCoughColds = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxTonsillitisPharyngitis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxNeckMassGoiter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxAsthma = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxTuberculosis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxPneumonia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxDifficultyBreathing = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHypertension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHypotension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxAnemia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxChestPainPalpitations = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHeartDisease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxBreastMass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxDiabetes = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxUlcerHyperacidity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxAbdominalPain = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxDiarrhea = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxKidneyDiseaseUti = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHernia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHemorrhoids = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxLossOfConsciousness = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxBackPainScoliosis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHeadacheMigraine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxDizzinessVertigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxEpilepsyConvulsion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxEasyFatigability = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxArthritisJointPain = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxFracture = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxMeasles = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxChickenPox = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxMumps = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxHepatitis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxMalaria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxStd = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxAllergies = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxSkinDisease = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxPreviousAccident = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxPreviousHospitalization = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxPreviousOperation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    HxOthers = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HxMedication = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FamCancer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FamHypertension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FamDiabetes = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FamOthers = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MenstrualPattern = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Lmp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Dysmenorrhea = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PregnantNow = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NumPregnancies = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NumMiscarriages = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Myoma = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OvarianCyst = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ObOthers = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SmokingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SmokingSince = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SticksPerDay = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DrinkingStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DrinkingSince = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Bottles = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DrinkingFrequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Bp1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Bp2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Pulse1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Pulse2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Temperature = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Height = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Weight = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BmiCategory = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VisionFarWithR = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VisionFarWithL = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VisionFarWithoutR = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VisionFarWithoutL = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Ishihara = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RosSkin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosHeadScalp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosEyes = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosEars = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosNose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosTeethThroat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosNeckThyroid = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosThoraxBreast = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosHeartLungs = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosAbdomen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosInguinalGenitalia = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosExtremitiesSpine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosTattoo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosMassCyst = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosOthers = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RosOthersNote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Findings = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LabChestXray = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LabChestXrayNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LabCbc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LabCbcNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LabUrinalysis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LabUrinalysisNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LabFecalysis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LabFecalysisNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Classification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Assessment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VitalSignsBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VitalSignsByLicense = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InterviewBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InterviewByLicense = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AssessmentBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssessmentByLicense = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NotedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NotedByLicense = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalExamReports", x => x.LabReportId);
                    table.ForeignKey(
                        name: "FK_MedicalExamReports_LabReports_LabReportId",
                        column: x => x.LabReportId,
                        principalTable: "LabReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // The module record and the billable service it belongs to (priced at 0: set the price in Services). Guarded like the other forms:
            // a fresh empty database gets its Modules from the legacy-data script, which refuses a non-empty target.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 33)
                BEGIN
                    DECLARE @serviceId bigint = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Medical Examination Report' ORDER BY Id);
                    IF @serviceId IS NULL
                    BEGIN
                        INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (N'Medical Examination Report', N'Medical Examination Report', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                        SET @serviceId = SCOPE_IDENTITY();
                    END
                    INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                    SELECT 33, ModuleTypeId, N'Medical Examination Report', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 16, 1, @serviceId FROM Modules WHERE Id = 6;
                END
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 33) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 33)
                    INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (33, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM UserPermissions WHERE ModuleId = 33;
                DELETE FROM ModuleDefaults WHERE ModuleId = 33;
                DELETE FROM LookupValues WHERE ModuleId = 33;
                DELETE FROM Modules WHERE Id = 33;");

            migrationBuilder.DropTable(
                name: "MedicalExamReports");
        }
    }
}
