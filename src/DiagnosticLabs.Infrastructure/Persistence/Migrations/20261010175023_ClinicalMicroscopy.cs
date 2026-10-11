using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalMicroscopy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalMicroscopyReports",
                columns: table => new
                {
                    LabReportId = table.Column<long>(type: "bigint", nullable: false),
                    ColorResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ColorReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransparencyResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransparencyReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PHResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PHReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SpecificGravityResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SpecificGravityReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProteinResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProteinReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GlucoseResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GlucoseReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BloodResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BloodReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LeukocytesResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LeukocytesReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NitriteResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NitriteReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BilirubinResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BilirubinReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UrobilinogenResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UrobilinogenReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KetonesResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    KetonesReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WBCResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WBCReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RBCResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RBCReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MucusThreadsResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MucusThreadsReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EpithelialCellsResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EpithelialCellsReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AmorphousUratesPhosphatesResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AmorphousUratesPhosphatesReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BacteriaResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BacteriaReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CrystalsResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CrystalsReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CastsResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CastsReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalMicroscopyReports", x => x.LabReportId);
                    table.ForeignKey(
                        name: "FK_ClinicalMicroscopyReports_LabReports_LabReportId",
                        column: x => x.LabReportId,
                        principalTable: "LabReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            // The module record, the billable service it belongs to (priced at 0: set the price in Services) and the reference ranges as saved defaults.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 6) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 30)
                BEGIN
                    DECLARE @serviceId bigint = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Clinical Microscopy' ORDER BY Id);
                    IF @serviceId IS NULL
                    BEGIN
                        INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (N'Clinical Microscopy', N'Clinical Microscopy', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                        SET @serviceId = SCOPE_IDENTITY();
                    END
                    INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                    SELECT 30, ModuleTypeId, N'Clinical Microscopy', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 15, 1, @serviceId FROM Modules WHERE Id = 6;
                END
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 30) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 30)
                    INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (30, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""PHReference""', N'5.0 - 8.0') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""PHReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""SpecificGravityReference""', N'1.005 - 1.030') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""SpecificGravityReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ProteinReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ProteinReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""GlucoseReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""GlucoseReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""LeukocytesReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""LeukocytesReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""NitriteReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""NitriteReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BilirubinReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BilirubinReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""UrobilinogenReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""UrobilinogenReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""KetonesReference""', N'NEGATIVE') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""KetonesReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""WBCReference""', N'0-5/hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""WBCReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""RBCReference""', N'0-2/hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""RBCReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CrystalsReference""', N'hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CrystalsReference""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CastsReference""', N'lpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CastsReference""') IS NULL;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM UserPermissions WHERE ModuleId = 30;
                DELETE FROM ModuleDefaults WHERE ModuleId = 30;
                DELETE FROM LookupValues WHERE ModuleId = 30;
                DELETE FROM Modules WHERE Id = 30;");

            migrationBuilder.DropTable(
                name: "ClinicalMicroscopyReports");
        }
    }
}
