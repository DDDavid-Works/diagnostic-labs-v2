using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HideMedicalExaminationModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Medical Examination" (14) and "Medical Examination Page 2" (15) are not used yet: hide them from the menu until the client says what they are.
            // The rows (and the services linked to them) stay, so nothing is lost; switch IsActive back on to show them again.
            migrationBuilder.Sql("UPDATE Modules SET IsActive = 0 WHERE Id IN (14, 15);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Modules SET IsActive = 1 WHERE Id IN (14, 15);");
        }
    }
}
