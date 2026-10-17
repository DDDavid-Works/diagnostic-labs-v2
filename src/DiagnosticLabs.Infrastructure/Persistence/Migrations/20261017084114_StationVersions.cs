using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StationVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StationVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StationVersions_StationName",
                table: "StationVersions",
                column: "StationName",
                unique: true);

            // The Stations screen (34) next to Users (27) in the administration group, whose menu only administrators see. It is a read-only list: view only.
            // The guard keeps a database without the legacy modules (a fresh one) from getting an orphan row.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 27) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 34)
                    INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                    SELECT 34, ModuleTypeId, N'Stations', 1, 0, 0, 0, 0, 0, 0, 0, Icon, SortOrder + 1, 1, NULL FROM Modules WHERE Id = 27;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM UserPermissions WHERE ModuleId = 34; DELETE FROM Modules WHERE Id = 34;");

            migrationBuilder.DropTable(
                name: "StationVersions");
        }
    }
}
