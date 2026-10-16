using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameFormDesignerModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The module (32) is called Custom Lab Form in the menu and in Users. Only the name the migration itself seeded is replaced: a name the client chose stays.
            migrationBuilder.Sql("UPDATE Modules SET ModuleName = N'Custom Lab Form' WHERE Id = 32 AND ModuleName = N'Form Designer';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Modules SET ModuleName = N'Form Designer' WHERE Id = 32 AND ModuleName = N'Custom Lab Form';");
        }
    }
}
