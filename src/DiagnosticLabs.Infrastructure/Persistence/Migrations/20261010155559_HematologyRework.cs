using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HematologyRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RBCCountNValue",
                table: "HematologyReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RBCCountResult",
                table: "HematologyReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // The client's reference values become what a new form starts with (only for fields that have no saved default yet).
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 7) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 7)
                    INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (7, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HematocritNormalValue""', N'MALE: 42.0-52.0%    FEMALE: 37.0-47.0%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HematocritNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HemoglobinNormalValue""', N'MALE: 140-170 g/L    FEMALE: 120-155 g/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HemoglobinNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""RBCCountNormalValue""', N'3.50-5.50 ' + NCHAR(215) + N'10' + NCHAR(185) + NCHAR(178) + N'/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""RBCCountNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""WBCCountNormalValue""', N'5.0-10.0 ' + NCHAR(215) + N'10' + NCHAR(8313) + N'/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""WBCCountNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""NeutrophilsNormalValue""', N'50.0-70.0%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""NeutrophilsNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""LymphocytesNormalValue""', N'10.0-40.0%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""LymphocytesNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""EosinophilsNormalValue""', N'0.0-0.05%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""EosinophilsNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""MonocytesNormalValue""', N'0.0-0.07%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""MonocytesNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BasophilsNormalValue""', N'0.0-0.01%') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BasophilsNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""PlateletCountNormalValue""', N'150-400 ' + NCHAR(215) + N'10' + NCHAR(8313) + N'/L') WHERE ModuleId = 7 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""PlateletCountNormalValue""') IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RBCCountNValue",
                table: "HematologyReports");

            migrationBuilder.DropColumn(
                name: "RBCCountResult",
                table: "HematologyReports");
        }
    }
}
