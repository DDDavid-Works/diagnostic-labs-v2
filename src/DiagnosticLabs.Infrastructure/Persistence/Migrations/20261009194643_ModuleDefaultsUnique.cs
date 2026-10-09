using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModuleDefaultsUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModuleDefaults_ModuleId",
                table: "ModuleDefaults");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDefaults_ModuleId",
                table: "ModuleDefaults",
                column: "ModuleId",
                unique: true,
                filter: "[ModuleId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModuleDefaults_ModuleId",
                table: "ModuleDefaults");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleDefaults_ModuleId",
                table: "ModuleDefaults",
                column: "ModuleId");
        }
    }
}
