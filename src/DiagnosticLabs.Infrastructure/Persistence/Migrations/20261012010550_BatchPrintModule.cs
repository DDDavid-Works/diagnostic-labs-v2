using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BatchPrintModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Batch Print module (31) under Reports, next to the Sales Report (24) whose flags it copies (print only). Administrators see it at once;
            // other users get the Print permission in Users. The guard keeps a database without the legacy modules (a fresh one) from getting an orphan row.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 24) AND NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 31)
                    INSERT INTO Modules (Id, ModuleTypeId, ModuleName, HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, SortOrder, IsActive, ServiceId)
                    SELECT 31, ModuleTypeId, N'Batch Print', HasView, HasCreate, HasEdit, HasDelete, HasSearch, HasPrint, HasShowList, HasSetDefaults, Icon, 3, 1, NULL FROM Modules WHERE Id = 24;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM UserPermissions WHERE ModuleId = 31; DELETE FROM Modules WHERE Id = 31;");
        }
    }
}
