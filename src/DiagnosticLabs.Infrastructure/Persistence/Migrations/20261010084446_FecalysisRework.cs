using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FecalysisRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bacteria",
                table: "StoolFecalysisReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatGlobules",
                table: "StoolFecalysisReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicalTechnologist2",
                table: "StoolFecalysisReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Others",
                table: "StoolFecalysisReports",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OvaParasite",
                table: "StoolFecalysisReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rbc",
                table: "StoolFecalysisReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Wbc",
                table: "StoolFecalysisReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YeastCells",
                table: "StoolFecalysisReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // The old form had a single free-text Result; the new one has Remarks. Move the text across so nothing is lost:
            // into Remarks when the report has none, otherwise into Others (the Result column itself stays for now).
            migrationBuilder.Sql(@"
                UPDATE r SET r.Remarks = s.Result
                FROM LabReports r JOIN StoolFecalysisReports s ON s.LabReportId = r.Id
                WHERE LTRIM(RTRIM(ISNULL(s.Result, N''))) <> N'' AND LTRIM(RTRIM(ISNULL(r.Remarks, N''))) = N'';

                UPDATE s SET s.Others = s.Result
                FROM StoolFecalysisReports s JOIN LabReports r ON r.Id = s.LabReportId
                WHERE LTRIM(RTRIM(ISNULL(s.Result, N''))) <> N'' AND r.Remarks <> s.Result AND s.Others = N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bacteria",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "FatGlobules",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "MedicalTechnologist2",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "Others",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "OvaParasite",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "Rbc",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "Wbc",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "YeastCells",
                table: "StoolFecalysisReports");
        }
    }
}
