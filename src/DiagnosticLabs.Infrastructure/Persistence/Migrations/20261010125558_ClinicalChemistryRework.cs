using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicalChemistryRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FBSNValue",
                table: "ClinicalChemistryReports",
                newName: "FastingBloodSugarCNValue");

            migrationBuilder.RenameColumn(
                name: "FBSResult",
                table: "ClinicalChemistryReports",
                newName: "FastingBloodSugarCResults");

            migrationBuilder.RenameColumn(
                name: "TotalCholesterolNValue",
                table: "ClinicalChemistryReports",
                newName: "CholesterolCNValue");

            migrationBuilder.RenameColumn(
                name: "TotalCholesterolResult",
                table: "ClinicalChemistryReports",
                newName: "CholesterolCResults");

            migrationBuilder.RenameColumn(
                name: "TriglyceridesNValue",
                table: "ClinicalChemistryReports",
                newName: "TriglyceridesCNValue");

            migrationBuilder.RenameColumn(
                name: "TriglyceridesResult",
                table: "ClinicalChemistryReports",
                newName: "TriglyceridesCResults");

            migrationBuilder.RenameColumn(
                name: "HDLNValue",
                table: "ClinicalChemistryReports",
                newName: "HDLCNValue");

            migrationBuilder.RenameColumn(
                name: "HDLResult",
                table: "ClinicalChemistryReports",
                newName: "HDLCResults");

            migrationBuilder.RenameColumn(
                name: "LDLNValue",
                table: "ClinicalChemistryReports",
                newName: "LDLCNValue");

            migrationBuilder.RenameColumn(
                name: "LDLResult",
                table: "ClinicalChemistryReports",
                newName: "LDLCResults");

            migrationBuilder.RenameColumn(
                name: "CreatinineNValue",
                table: "ClinicalChemistryReports",
                newName: "CreatinineCNValue");

            migrationBuilder.RenameColumn(
                name: "CreatinineResult",
                table: "ClinicalChemistryReports",
                newName: "CreatinineCResults");

            migrationBuilder.RenameColumn(
                name: "BUNNValue",
                table: "ClinicalChemistryReports",
                newName: "BloodUreaNitrogenCNValue");

            migrationBuilder.RenameColumn(
                name: "BUNResult",
                table: "ClinicalChemistryReports",
                newName: "BloodUreaNitrogenCResults");

            migrationBuilder.RenameColumn(
                name: "BloodUricAcidNValue",
                table: "ClinicalChemistryReports",
                newName: "BloodUricAcidCNValue");

            migrationBuilder.RenameColumn(
                name: "BloodUricAcidResult",
                table: "ClinicalChemistryReports",
                newName: "BloodUricAcidCResults");

            migrationBuilder.RenameColumn(
                name: "ALTSGPTNValue",
                table: "ClinicalChemistryReports",
                newName: "ALTSGPTCNValue");

            migrationBuilder.RenameColumn(
                name: "ALTSGPTResult",
                table: "ClinicalChemistryReports",
                newName: "ALTSGPTCResults");

            migrationBuilder.AddColumn<string>(
                name: "FastingBloodSugarCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FastingBloodSugarSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FastingBloodSugarSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FastingBloodSugarSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CholesterolCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CholesterolSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CholesterolSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CholesterolSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriglyceridesCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriglyceridesSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriglyceridesSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriglyceridesSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HDLCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HDLSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HDLSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HDLSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LDLCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LDLSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LDLSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LDLSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatinineCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatinineSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatinineSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatinineSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUreaNitrogenCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUreaNitrogenSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUreaNitrogenSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUreaNitrogenSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUricAcidCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUricAcidSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUricAcidSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodUricAcidSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ALTSGPTCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ALTSGPTSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ALTSGPTSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ALTSGPTSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASTSGOTCNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASTSGOTCUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASTSGOTCResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASTSGOTSNValue",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASTSGOTSUnit",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASTSGOTSResults",
                table: "ClinicalChemistryReports",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // The saved defaults of this screen name the fields too, and the client's units and reference ranges become what a new form starts with.
            migrationBuilder.Sql(@"
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""FBSNormalValue""', N'""FastingBloodSugarConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""TotalCholesterolNormalValue""', N'""CholesterolConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""TriglyceridesNormalValue""', N'""TriglyceridesConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""HDLNormalValue""', N'""HDLConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""LDLNormalValue""', N'""LDLConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""CreatinineNormalValue""', N'""CreatinineConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""BUNNormalValue""', N'""BloodUreaNitrogenConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""BloodUricAcidNormalValue""', N'""BloodUricAcidConventionalNormalValue""') WHERE ModuleId = 11;
                UPDATE ModuleDefaults SET Defaults = REPLACE(Defaults, N'""ALTSGPTNormalValue""', N'""ALTSGPTConventionalNormalValue""') WHERE ModuleId = 11;
                IF EXISTS (SELECT 1 FROM Modules WHERE Id = 11) AND NOT EXISTS (SELECT 1 FROM ModuleDefaults WHERE ModuleId = 11)
                    INSERT INTO ModuleDefaults (ModuleId, Defaults, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (11, N'{}', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""FastingBloodSugarConventionalNormalValue""', N'60-121') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""FastingBloodSugarConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""FastingBloodSugarConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""FastingBloodSugarConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""FastingBloodSugarSystemNormalValue""', N'3.34-6.73') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""FastingBloodSugarSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""FastingBloodSugarSystemUnit""', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""FastingBloodSugarSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CholesterolConventionalNormalValue""', N'50-200') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CholesterolConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CholesterolConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CholesterolConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CholesterolSystemNormalValue""', N'1.29-5.18') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CholesterolSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CholesterolSystemUnit""', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CholesterolSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""TriglyceridesConventionalNormalValue""', N'0-150') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""TriglyceridesConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""TriglyceridesConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""TriglyceridesConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""TriglyceridesSystemNormalValue""', N'0-1.70') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""TriglyceridesSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""TriglyceridesSystemUnit""', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""TriglyceridesSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HDLConventionalNormalValue""', N'35-80') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HDLConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HDLConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HDLConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HDLSystemNormalValue""', N'0.91-2.08') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HDLSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""HDLSystemUnit""', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""HDLSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""LDLConventionalNormalValue""', N'66-178') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""LDLConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""LDLConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""LDLConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""LDLSystemNormalValue""', N'1.72-4.63') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""LDLSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""LDLSystemUnit""', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""LDLSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CreatinineConventionalNormalValue""', N'0.70-1.40') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CreatinineConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CreatinineConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CreatinineConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CreatinineSystemNormalValue""', N'62.0-124.0') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CreatinineSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""CreatinineSystemUnit""', N'umol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""CreatinineSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUreaNitrogenConventionalNormalValue""', N'7.80-20.17') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUreaNitrogenConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUreaNitrogenConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUreaNitrogenConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUreaNitrogenSystemNormalValue""', N'2.8-7.2') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUreaNitrogenSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUreaNitrogenSystemUnit""', N'mmol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUreaNitrogenSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUricAcidConventionalNormalValue""', N'2.35-7.06') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUricAcidConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUricAcidConventionalUnit""', N'mg/dL') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUricAcidConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUricAcidSystemNormalValue""', N'0.14-0.42') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUricAcidSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""BloodUricAcidSystemUnit""', N'umol/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""BloodUricAcidSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ALTSGPTConventionalNormalValue""', N'0-32') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ALTSGPTConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ALTSGPTConventionalUnit""', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ALTSGPTConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ALTSGPTSystemNormalValue""', N'0-32') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ALTSGPTSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ALTSGPTSystemUnit""', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ALTSGPTSystemUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ASTSGOTConventionalNormalValue""', N'0-31') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ASTSGOTConventionalNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ASTSGOTConventionalUnit""', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ASTSGOTConventionalUnit""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ASTSGOTSystemNormalValue""', N'0-31') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ASTSGOTSystemNormalValue""') IS NULL;
                UPDATE ModuleDefaults SET Defaults = JSON_MODIFY(Defaults, '$.""ASTSGOTSystemUnit""', N'U/L') WHERE ModuleId = 11 AND ISJSON(Defaults) = 1 AND JSON_VALUE(Defaults, '$.""ASTSGOTSystemUnit""') IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FastingBloodSugarCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "FastingBloodSugarSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "FastingBloodSugarSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "FastingBloodSugarSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CholesterolCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CholesterolSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CholesterolSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CholesterolSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "TriglyceridesCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "TriglyceridesSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "TriglyceridesSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "TriglyceridesSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "HDLCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "HDLSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "HDLSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "HDLSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "LDLCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "LDLSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "LDLSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "LDLSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CreatinineCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CreatinineSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CreatinineSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "CreatinineSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUreaNitrogenCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUreaNitrogenSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUreaNitrogenSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUreaNitrogenSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUricAcidCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUricAcidSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUricAcidSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "BloodUricAcidSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ALTSGPTCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ALTSGPTSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ALTSGPTSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ALTSGPTSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ASTSGOTCNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ASTSGOTCUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ASTSGOTCResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ASTSGOTSNValue",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ASTSGOTSUnit",
                table: "ClinicalChemistryReports");

            migrationBuilder.DropColumn(
                name: "ASTSGOTSResults",
                table: "ClinicalChemistryReports");

            migrationBuilder.RenameColumn(
                name: "FastingBloodSugarCNValue",
                table: "ClinicalChemistryReports",
                newName: "FBSNValue");

            migrationBuilder.RenameColumn(
                name: "FastingBloodSugarCResults",
                table: "ClinicalChemistryReports",
                newName: "FBSResult");

            migrationBuilder.RenameColumn(
                name: "CholesterolCNValue",
                table: "ClinicalChemistryReports",
                newName: "TotalCholesterolNValue");

            migrationBuilder.RenameColumn(
                name: "CholesterolCResults",
                table: "ClinicalChemistryReports",
                newName: "TotalCholesterolResult");

            migrationBuilder.RenameColumn(
                name: "TriglyceridesCNValue",
                table: "ClinicalChemistryReports",
                newName: "TriglyceridesNValue");

            migrationBuilder.RenameColumn(
                name: "TriglyceridesCResults",
                table: "ClinicalChemistryReports",
                newName: "TriglyceridesResult");

            migrationBuilder.RenameColumn(
                name: "HDLCNValue",
                table: "ClinicalChemistryReports",
                newName: "HDLNValue");

            migrationBuilder.RenameColumn(
                name: "HDLCResults",
                table: "ClinicalChemistryReports",
                newName: "HDLResult");

            migrationBuilder.RenameColumn(
                name: "LDLCNValue",
                table: "ClinicalChemistryReports",
                newName: "LDLNValue");

            migrationBuilder.RenameColumn(
                name: "LDLCResults",
                table: "ClinicalChemistryReports",
                newName: "LDLResult");

            migrationBuilder.RenameColumn(
                name: "CreatinineCNValue",
                table: "ClinicalChemistryReports",
                newName: "CreatinineNValue");

            migrationBuilder.RenameColumn(
                name: "CreatinineCResults",
                table: "ClinicalChemistryReports",
                newName: "CreatinineResult");

            migrationBuilder.RenameColumn(
                name: "BloodUreaNitrogenCNValue",
                table: "ClinicalChemistryReports",
                newName: "BUNNValue");

            migrationBuilder.RenameColumn(
                name: "BloodUreaNitrogenCResults",
                table: "ClinicalChemistryReports",
                newName: "BUNResult");

            migrationBuilder.RenameColumn(
                name: "BloodUricAcidCNValue",
                table: "ClinicalChemistryReports",
                newName: "BloodUricAcidNValue");

            migrationBuilder.RenameColumn(
                name: "BloodUricAcidCResults",
                table: "ClinicalChemistryReports",
                newName: "BloodUricAcidResult");

            migrationBuilder.RenameColumn(
                name: "ALTSGPTCNValue",
                table: "ClinicalChemistryReports",
                newName: "ALTSGPTNValue");

            migrationBuilder.RenameColumn(
                name: "ALTSGPTCResults",
                table: "ClinicalChemistryReports",
                newName: "ALTSGPTResult");

        }
    }
}
