using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientFieldRenames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Albumin",
                table: "UrinalysisReports",
                newName: "Protein");

            migrationBuilder.RenameColumn(
                name: "Sugar",
                table: "UrinalysisReports",
                newName: "Glucose");

            migrationBuilder.RenameColumn(
                name: "SegmentersNValue",
                table: "HematologyReports",
                newName: "NeutrophilsNValue");

            migrationBuilder.RenameColumn(
                name: "SegmentersResult",
                table: "HematologyReports",
                newName: "NeutrophilsResult");

            migrationBuilder.RenameColumn(
                name: "SGPTNValue",
                table: "ClinicalChemistryReports",
                newName: "ALTSGPTNValue");

            migrationBuilder.RenameColumn(
                name: "SGPTResult",
                table: "ClinicalChemistryReports",
                newName: "ALTSGPTResult");

            migrationBuilder.RenameColumn(
                name: "SGOTCNValue",
                table: "ClinicalChemistry2Reports",
                newName: "ASTSGOTCNValue");

            migrationBuilder.RenameColumn(
                name: "SGOTCUnit",
                table: "ClinicalChemistry2Reports",
                newName: "ASTSGOTCUnit");

            migrationBuilder.RenameColumn(
                name: "SGOTCResults",
                table: "ClinicalChemistry2Reports",
                newName: "ASTSGOTCResults");

            migrationBuilder.RenameColumn(
                name: "SGOTSNValue",
                table: "ClinicalChemistry2Reports",
                newName: "ASTSGOTSNValue");

            migrationBuilder.RenameColumn(
                name: "SGOTSUnit",
                table: "ClinicalChemistry2Reports",
                newName: "ASTSGOTSUnit");

            migrationBuilder.RenameColumn(
                name: "SGOTSResults",
                table: "ClinicalChemistry2Reports",
                newName: "ASTSGOTSResults");

            // The entry lists and the saved defaults of these screens name their fields too; rename them along with the columns.
            migrationBuilder.Sql(@"
                UPDATE LookupValues SET FieldName = N'Protein' WHERE ModuleId = 6 AND FieldName = N'Albumin';
                UPDATE LookupValues SET FieldName = N'Glucose' WHERE ModuleId = 6 AND FieldName = N'Sugar';
                UPDATE LookupValues SET FieldName = N'Reaction (PH)' WHERE ModuleId = 6 AND FieldName = N'Reaction';
                UPDATE ModuleDefaults SET Defaults = REPLACE(REPLACE(Defaults, N'""Albumin""', N'""Protein""'), N'""Sugar""', N'""Glucose""') WHERE ModuleId = 6;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""Segmenters', N'""Neutrophils') WHERE ModuleId = 7;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""SGPT', N'""ALTSGPT') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""SGOT', N'""ASTSGOT') WHERE ModuleId = 13;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Protein",
                table: "UrinalysisReports",
                newName: "Albumin");

            migrationBuilder.RenameColumn(
                name: "Glucose",
                table: "UrinalysisReports",
                newName: "Sugar");

            migrationBuilder.RenameColumn(
                name: "NeutrophilsNValue",
                table: "HematologyReports",
                newName: "SegmentersNValue");

            migrationBuilder.RenameColumn(
                name: "NeutrophilsResult",
                table: "HematologyReports",
                newName: "SegmentersResult");

            migrationBuilder.RenameColumn(
                name: "ALTSGPTNValue",
                table: "ClinicalChemistryReports",
                newName: "SGPTNValue");

            migrationBuilder.RenameColumn(
                name: "ALTSGPTResult",
                table: "ClinicalChemistryReports",
                newName: "SGPTResult");

            migrationBuilder.RenameColumn(
                name: "ASTSGOTCNValue",
                table: "ClinicalChemistry2Reports",
                newName: "SGOTCNValue");

            migrationBuilder.RenameColumn(
                name: "ASTSGOTCUnit",
                table: "ClinicalChemistry2Reports",
                newName: "SGOTCUnit");

            migrationBuilder.RenameColumn(
                name: "ASTSGOTCResults",
                table: "ClinicalChemistry2Reports",
                newName: "SGOTCResults");

            migrationBuilder.RenameColumn(
                name: "ASTSGOTSNValue",
                table: "ClinicalChemistry2Reports",
                newName: "SGOTSNValue");

            migrationBuilder.RenameColumn(
                name: "ASTSGOTSUnit",
                table: "ClinicalChemistry2Reports",
                newName: "SGOTSUnit");

            migrationBuilder.RenameColumn(
                name: "ASTSGOTSResults",
                table: "ClinicalChemistry2Reports",
                newName: "SGOTSResults");

            migrationBuilder.Sql(@"
                UPDATE LookupValues SET FieldName = N'Albumin' WHERE ModuleId = 6 AND FieldName = N'Protein';
                UPDATE LookupValues SET FieldName = N'Sugar' WHERE ModuleId = 6 AND FieldName = N'Glucose';
                UPDATE LookupValues SET FieldName = N'Reaction' WHERE ModuleId = 6 AND FieldName = N'Reaction (PH)';
                UPDATE ModuleDefaults SET Defaults = REPLACE(REPLACE(Defaults, N'""Protein""', N'""Albumin""'), N'""Glucose""', N'""Sugar""') WHERE ModuleId = 6;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""Neutrophils', N'""Segmenters') WHERE ModuleId = 7;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""ALTSGPT', N'""SGPT') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""ASTSGOT', N'""SGOT') WHERE ModuleId = 13;");
        }
    }
}
