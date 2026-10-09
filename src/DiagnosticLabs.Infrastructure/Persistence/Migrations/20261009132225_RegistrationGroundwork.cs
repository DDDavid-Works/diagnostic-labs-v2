using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationGroundwork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Companies");

            migrationBuilder.AddColumn<string>(
                name: "Age",
                table: "Patients",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Age",
                table: "Patients");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
