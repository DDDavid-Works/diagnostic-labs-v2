using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhysicalExamination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhysicalExaminationReports",
                columns: table => new
                {
                    LabReportId = table.Column<long>(type: "bigint", nullable: false),
                    CbcDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BloodTypingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UrinalysisDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FecalysisDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HematocritNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HematocritFemaleNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HematocritResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HemoglobinNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HemoglobinFemaleNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HemoglobinResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WBCCountNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WBCCountResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SegmentersNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SegmentersResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LymphocytesNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LymphocytesResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EosinophilsNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EosinophilsResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MonocytesNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MonocytesResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BasophilsNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BasophilsResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StabNValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StabResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BloodTyping = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RhTyping = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineColor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineAppearance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineReaction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineSPGravity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineAlbumin = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineSugar = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrinePusCells = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineRedCells = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineMucusThreads = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineEpithelialCells = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineAmorphousUratesPO4 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineBacteria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineCrystals = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineCasts = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FecalysisColor = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FecalysisConsistency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrineOthers = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FecalysisResult = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Others = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalExaminationReports", x => x.LabReportId);
                    table.ForeignKey(
                        name: "FK_PhysicalExaminationReports_LabReports_LabReportId",
                        column: x => x.LabReportId,
                        principalTable: "LabReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            // The module record, the billable service it belongs to (priced at 0: set the price in Services) and the normal values of the blood count as saved defaults.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 29)
                BEGIN
                    DECLARE @serviceId bigint = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Physical Examination' ORDER BY Id);
                    IF @serviceId IS NULL
                    BEGIN
                        INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (N'Physical Examination', N'Physical Examination', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                        SET @serviceId = SCOPE_IDENTITY();
                    END
                    INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                    SELECT 29, ModuleTypeId, N'Physical Examination', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 14, 1, @serviceId FROM Modules WHERE Id = 6;
                END
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 29) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 29)
                    INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (29, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HematocritNValue""', N'0.42 - 0.52') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HematocritNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HematocritFemaleNValue""', N'0.37 - 0.47') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HematocritFemaleNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HemoglobinNValue""', N'140 - 170 g/l') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HemoglobinNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HemoglobinFemaleNValue""', N'120 - 150 g/l') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HemoglobinFemaleNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""WBCCountNValue""', N'5 - 10 x 10' + NCHAR(8313) + N'/L') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""WBCCountNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""SegmentersNValue""', N'0.50 - 0.70') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""SegmentersNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""LymphocytesNValue""', N'0.10 - 0.40') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""LymphocytesNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""EosinophilsNValue""', N'0 - 0.05') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""EosinophilsNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""MonocytesNValue""', N'0 - 0.07') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""MonocytesNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BasophilsNValue""', N'0 - 0.01') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BasophilsNValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""StabNValue""', N'0 - 0.05') WHERE ModuleId = 29 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""StabNValue""') IS NULL;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM UserPermissions WHERE ModuleId = 29;
                DELETE FROM ModuleDefaults WHERE ModuleId = 29;
                DELETE FROM LookupValues WHERE ModuleId = 29;
                DELETE FROM Modules WHERE Id = 29;");

            migrationBuilder.DropTable(
                name: "PhysicalExaminationReports");
        }
    }
}
