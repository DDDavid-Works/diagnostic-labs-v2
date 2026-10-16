using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomForms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomForms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomForms_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomForms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomForms_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomFormVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomFormId = table.Column<long>(type: "bigint", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFormVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFormVersions_CustomForms_CustomFormId",
                        column: x => x.CustomFormId,
                        principalTable: "CustomForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomFormVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomFormVersions_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomFormReports",
                columns: table => new
                {
                    LabReportId = table.Column<long>(type: "bigint", nullable: false),
                    CustomFormVersionId = table.Column<long>(type: "bigint", nullable: false),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFormReports", x => x.LabReportId);
                    table.ForeignKey(
                        name: "FK_CustomFormReports_CustomFormVersions_CustomFormVersionId",
                        column: x => x.CustomFormVersionId,
                        principalTable: "CustomFormVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomFormReports_LabReports_LabReportId",
                        column: x => x.LabReportId,
                        principalTable: "LabReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormReports_CustomFormVersionId",
                table: "CustomFormReports",
                column: "CustomFormVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomForms_ModuleId",
                table: "CustomForms",
                column: "ModuleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormVersions_CustomFormId_VersionNumber",
                table: "CustomFormVersions",
                columns: new[] { "CustomFormId", "VersionNumber" },
                unique: true);

            // The Form Designer module (32) under Management, next to Services (19) whose type and icon it copies. It is a place to view, create and edit
            // designs: a form is never deleted (it is switched off), so there is no Delete. Administrators see it at once; other users are granted it in
            // Users. The guard keeps a database without the legacy modules (a fresh one) from getting an orphan row. The forms the client makes get their
            // own module rows from id 1000 up (ModuleIds.FirstCustomForm).
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 19) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 32)
                    INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                    SELECT 32, ModuleTypeId, N'Form Designer', 1, 1, 1, 0, 0, 0, 0, 0, Icon, 8, 1, NULL FROM Modules WHERE Id = 19;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Forms made in the designer go with their tables: their results, entry lists, saved defaults and permissions are removed first.
            migrationBuilder.Sql(@"
                DELETE FROM LabReports WHERE ReportType = 'CustomForm';
                DELETE FROM UserPermissions WHERE ModuleId = 32 OR ModuleId IN (SELECT ModuleId FROM CustomForms);
                DELETE FROM ModuleDefaults WHERE ModuleId IN (SELECT ModuleId FROM CustomForms);
                DELETE FROM LookupValues WHERE ModuleId IN (SELECT ModuleId FROM CustomForms);
            ");

            migrationBuilder.DropTable(
                name: "CustomFormReports");

            migrationBuilder.DropTable(
                name: "CustomFormVersions");

            migrationBuilder.DropTable(
                name: "CustomForms");

            migrationBuilder.Sql("DELETE FROM Modules WHERE Id = 32 OR Id >= 1000;");
        }
    }
}
