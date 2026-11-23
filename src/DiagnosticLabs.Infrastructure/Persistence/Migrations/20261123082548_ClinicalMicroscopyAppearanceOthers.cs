using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Clinical Microscopy reworked from the client's new sketch (2026-11-23): Transparency becomes Appearance (columns, entry list and saved defaults), a
    /// new Others line (a result only), Amorphous Urates/Phosphates is now labelled Amorphous Urates/PO4 (its entry list follows the label), and the new
    /// reference ranges (pH 5.0 - 7.5, WBC 0-3/hpf, Crystals /hpf, Casts /lpf) replace the saved defaults where they still hold the old seeded values.
    /// Written by hand: EF paired TransparencyResult with OthersResult.
    /// </summary>
    public partial class ClinicalMicroscopyAppearanceOthers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "TransparencyResult", table: "ClinicalMicroscopyReports", newName: "AppearanceResult");
            migrationBuilder.RenameColumn(name: "TransparencyReference", table: "ClinicalMicroscopyReports", newName: "AppearanceReference");

            migrationBuilder.AddColumn<string>(
                name: "OthersResult",
                table: "ClinicalMicroscopyReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE LookupValues SET FieldName = N'Appearance' WHERE ModuleId = 30 AND FieldName = N'Transparency';
                UPDATE LookupValues SET FieldName = N'Amorphous Urates/PO4' WHERE ModuleId = 30 AND FieldName = N'Amorphous Urates/Phosphates';
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""TransparencyReference""', N'""AppearanceReference""') WHERE ModuleId = 30;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""PHReference""', N'5.0 - 7.5') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""PHReference""') = N'5.0 - 8.0';
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""WBCReference""', N'0-3/hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""WBCReference""') = N'0-5/hpf';
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CrystalsReference""', N'/hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CrystalsReference""') = N'hpf';
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CastsReference""', N'/lpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CastsReference""') = N'lpf';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""PHReference""', N'5.0 - 8.0') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""PHReference""') = N'5.0 - 7.5';
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""WBCReference""', N'0-5/hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""WBCReference""') = N'0-3/hpf';
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CrystalsReference""', N'hpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CrystalsReference""') = N'/hpf';
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CastsReference""', N'lpf') WHERE ModuleId = 30 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CastsReference""') = N'/lpf';
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""AppearanceReference""', N'""TransparencyReference""') WHERE ModuleId = 30;
                UPDATE LookupValues SET FieldName = N'Amorphous Urates/Phosphates' WHERE ModuleId = 30 AND FieldName = N'Amorphous Urates/PO4';
                UPDATE LookupValues SET FieldName = N'Transparency' WHERE ModuleId = 30 AND FieldName = N'Appearance';");

            migrationBuilder.DropColumn(name: "OthersResult", table: "ClinicalMicroscopyReports");
            migrationBuilder.RenameColumn(name: "AppearanceResult", table: "ClinicalMicroscopyReports", newName: "TransparencyResult");
            migrationBuilder.RenameColumn(name: "AppearanceReference", table: "ClinicalMicroscopyReports", newName: "TransparencyReference");
        }
    }
}
