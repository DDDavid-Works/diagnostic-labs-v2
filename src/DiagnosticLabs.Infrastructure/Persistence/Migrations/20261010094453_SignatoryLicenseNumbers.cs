using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SignatoryLicenseNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MedicalTechnologist2License",
                table: "StoolFecalysisReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNo",
                table: "LookupValues",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicalTechnologistLicense",
                table: "LabReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PathologistLicense",
                table: "LabReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MedicalTechnologist2License",
                table: "StoolFecalysisReports");

            migrationBuilder.DropColumn(
                name: "LicenseNo",
                table: "LookupValues");

            migrationBuilder.DropColumn(
                name: "MedicalTechnologistLicense",
                table: "LabReports");

            migrationBuilder.DropColumn(
                name: "PathologistLicense",
                table: "LabReports");
        }
    }
}
