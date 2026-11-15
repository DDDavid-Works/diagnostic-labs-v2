using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceFormSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResultModuleId",
                table: "PatientRegistrationServices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceFormSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<long>(type: "bigint", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFormSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceFormSections_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceFormSections_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFormSections_ModuleId",
                table: "ServiceFormSections",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFormSections_ServiceId_ModuleId",
                table: "ServiceFormSections",
                columns: new[] { "ServiceId", "ModuleId" },
                unique: true);

            migrationBuilder.Sql(SectionsSql);
        }

        /// <summary>
        /// Complete Blood Count (the client's own service when it exists) is entered in the CBC section of the Physical Examination sheet;
        /// Urinalysis and Stool/Fecalysis can be entered there too. The sheet's own service is no longer billed (its sections are), so it is
        /// switched off; older registrations keep it. The same SQL runs at the end of <c>db/migrate-from-legacy.sql</c> (tools/Generate-LegacyMigration.ps1
        /// reads it from here), since the client's data is imported after the migrations.
        /// </summary>
        internal const string SectionsSql = """
            IF EXISTS (SELECT 1 FROM Modules WHERE Id = 29)
            BEGIN
                DECLARE @cbcService bigint = (SELECT TOP 1 Id FROM Services WHERE ServiceName = N'Complete Blood Count' ORDER BY Id);
                IF @cbcService IS NULL
                BEGIN
                    INSERT INTO Services (ServiceName, ServiceDescription, Price, CreatedAtUtc, UpdatedAtUtc, IsActive) VALUES (N'Complete Blood Count', N'Complete Blood Count', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
                    SET @cbcService = SCOPE_IDENTITY();
                END;
                DECLARE @urineService bigint = (SELECT ServiceId FROM Modules WHERE Id = 6);
                DECLARE @stoolService bigint = (SELECT ServiceId FROM Modules WHERE Id = 5);
                INSERT INTO ServiceFormSections (ServiceId, ModuleId, Section)
                SELECT x.ServiceId, 29, x.Section FROM (VALUES (@cbcService, N'Cbc'), (@urineService, N'Urinalysis'), (@stoolService, N'Fecalysis')) x(ServiceId, Section)
                WHERE x.ServiceId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ServiceFormSections s WHERE s.ServiceId = x.ServiceId AND s.ModuleId = 29);
                UPDATE Services SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = (SELECT ServiceId FROM Modules WHERE Id = 29);
            END;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The Complete Blood Count service stays (it may be the client's own); the sheet's service is billed again.
            migrationBuilder.Sql("UPDATE Services SET IsActive = 1, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = (SELECT ServiceId FROM Modules WHERE Id = 29);");

            migrationBuilder.DropTable(
                name: "ServiceFormSections");

            migrationBuilder.DropColumn(
                name: "ResultModuleId",
                table: "PatientRegistrationServices");
        }
    }
}
