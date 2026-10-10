using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedSecondTechnologist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MedicalTechnologist2",
                table: "LabReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicalTechnologist2License",
                table: "LabReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // The second medical technologist moves from the Fecalysis detail to the shared header (other forms are signed by two as well).
            migrationBuilder.Sql(@"
                UPDATE r SET r.MedicalTechnologist2 = s.MedicalTechnologist2, r.MedicalTechnologist2License = s.MedicalTechnologist2License
                FROM LabReports r JOIN StoolFecalysisReports s ON s.LabReportId = r.Id;");

            migrationBuilder.DropColumn(
                name: "MedicalTechnologist2",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "MedicalTechnologist2License",
                table: "StoolFecalysisReports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MedicalTechnologist2",
                table: "StoolFecalysisReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicalTechnologist2License",
                table: "StoolFecalysisReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE s SET s.MedicalTechnologist2 = r.MedicalTechnologist2, s.MedicalTechnologist2License = r.MedicalTechnologist2License
                FROM StoolFecalysisReports s JOIN LabReports r ON r.Id = s.LabReportId;");

            migrationBuilder.DropColumn(
                name: "MedicalTechnologist2",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "MedicalTechnologist2License",
                table: "LabReports");
        }
    }
}
