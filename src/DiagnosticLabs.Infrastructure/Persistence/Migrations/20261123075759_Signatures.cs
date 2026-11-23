using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Signatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignatoryImages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LookupValueId = table.Column<long>(type: "bigint", nullable: false),
                    Image = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatoryImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatoryImages_LookupValues_LookupValueId",
                        column: x => x.LookupValueId,
                        principalTable: "LookupValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignatoryImages_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SignatoryImages_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignatoryImages_LookupValueId",
                table: "SignatoryImages",
                column: "LookupValueId",
                unique: true);

            migrationBuilder.Sql(SignaturesSql);
        }

        /// <summary>
        /// The Signatures screen (35) after Stations (34) in the administration group, whose menu only administrators see; and the shared Physician
        /// list, which the Annual PE (Page 2) and the Medical Examination Report pick the physician from. Physicians were picked from the Pathologist
        /// list until now, so it starts as a copy of that list (with the licences), plus the names of the Annual PE's own Physician Name list
        /// that are not on it (licence from a pathologist of the same name). Both steps run once only.
        /// The legacy migration script runs this same SQL (tools/Generate-LegacyMigration.ps1 reads it from here).
        /// </summary>
        internal const string SignaturesSql = """
            IF EXISTS (SELECT 1 FROM Modules WHERE Id = 34) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 35)
                INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                SELECT 35, ModuleTypeId, N'Signatures', 1, 0, 1, 0, 0, 0, 0, 0, N'signature', SortOrder + 1, 1, NULL FROM Modules WHERE Id = 34;
            IF NOT EXISTS (SELECT 1 FROM LookupValues WHERE ModuleId IS NULL AND Kind = 2 AND FieldName = N'Physician')
            BEGIN
                INSERT INTO LookupValues (ModuleId, Kind, FieldName, Title, Value, LicenseNo, CreatedAtUtc, UpdatedAtUtc, IsActive)
                SELECT NULL, 2, N'Physician', NULL, p.Value, p.LicenseNo, SYSUTCDATETIME(), SYSUTCDATETIME(), 1
                FROM LookupValues p
                WHERE p.ModuleId IS NULL AND p.Kind = 2 AND p.FieldName = N'Pathologist' AND p.IsActive = 1
                ORDER BY p.Id;
                INSERT INTO LookupValues (ModuleId, Kind, FieldName, Title, Value, LicenseNo, CreatedAtUtc, UpdatedAtUtc, IsActive)
                SELECT NULL, 2, N'Physician', NULL, LTRIM(RTRIM(n.Value)),
                       (SELECT TOP 1 p.LicenseNo FROM LookupValues p
                        WHERE p.ModuleId IS NULL AND p.Kind = 2 AND p.FieldName = N'Pathologist' AND p.IsActive = 1 AND LTRIM(RTRIM(p.Value)) = LTRIM(RTRIM(n.Value))
                        ORDER BY p.Id),
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 1
                FROM LookupValues n
                WHERE n.ModuleId = 16 AND n.Kind = 2 AND n.FieldName = N'Physician Name' AND n.IsActive = 1 AND LTRIM(RTRIM(n.Value)) <> N''
                  AND NOT EXISTS (SELECT 1 FROM LookupValues x
                                  WHERE x.ModuleId IS NULL AND x.Kind = 2 AND x.FieldName = N'Physician' AND LTRIM(RTRIM(x.Value)) = LTRIM(RTRIM(n.Value)))
                GROUP BY LTRIM(RTRIM(n.Value))
                ORDER BY MIN(n.Id);
            END
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignatoryImages");

            migrationBuilder.Sql(
                "DELETE FROM UserPermissions WHERE ModuleId = 35; DELETE FROM Modules WHERE Id = 35; " +
                "DELETE FROM LookupValues WHERE ModuleId IS NULL AND Kind = 2 AND FieldName = N'Physician';");
        }
    }
}
