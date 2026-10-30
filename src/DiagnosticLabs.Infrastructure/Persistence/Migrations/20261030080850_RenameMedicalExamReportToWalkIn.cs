using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Medical Examination Report (module 33) is called "Physical Examination (Walk-In)" in the app: its menu entry and its billable service are
    /// renamed (the service only while it still has the old name, so a name the client gave it is kept). The printout keeps its title.
    /// </summary>
    public partial class RenameMedicalExamReportToWalkIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE s SET ServiceName = N'Physical Examination (Walk-In)', ServiceDescription = N'Physical Examination (Walk-In)', UpdatedAtUtc = SYSUTCDATETIME()
                FROM Services s JOIN Modules m ON m.ServiceId = s.Id
                WHERE m.Id = 33 AND s.ServiceName = N'Medical Examination Report';
                UPDATE Modules SET ModuleName = N'Physical Examination (Walk-In)' WHERE Id = 33 AND ModuleName = N'Medical Examination Report';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE s SET ServiceName = N'Medical Examination Report', ServiceDescription = N'Medical Examination Report', UpdatedAtUtc = SYSUTCDATETIME()
                FROM Services s JOIN Modules m ON m.ServiceId = s.Id
                WHERE m.Id = 33 AND s.ServiceName = N'Physical Examination (Walk-In)';
                UPDATE Modules SET ModuleName = N'Medical Examination Report' WHERE Id = 33 AND ModuleName = N'Physical Examination (Walk-In)';
                """);
        }
    }
}
