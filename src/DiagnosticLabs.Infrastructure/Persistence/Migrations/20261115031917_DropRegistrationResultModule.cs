using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The choice of form kept on a registration line (Urinalysis on its own form or on the Physical Examination sheet) is dropped: it was kept
    /// when the user picked a form, before any result was saved, so the home chip stopped asking. The chip now asks until a result is saved.
    /// </summary>
    public partial class DropRegistrationResultModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResultModuleId",
                table: "PatientRegistrationServices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResultModuleId",
                table: "PatientRegistrationServices",
                type: "int",
                nullable: true);
        }
    }
}
