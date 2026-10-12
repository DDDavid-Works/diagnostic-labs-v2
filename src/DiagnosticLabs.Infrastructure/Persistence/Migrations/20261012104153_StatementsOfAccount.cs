using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StatementsOfAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatementsOfAccount",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatementNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BatchName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Attention = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PatientId = table.Column<long>(type: "bigint", nullable: true),
                    PatientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PatientCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: true),
                    GrossTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PaidTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BalanceTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IsVoid = table.Column<bool>(type: "bit", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    VoidedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RegeneratedFromId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementsOfAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementsOfAccount_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatementsOfAccount_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatementsOfAccount_StatementsOfAccount_RegeneratedFromId",
                        column: x => x.RegeneratedFromId,
                        principalTable: "StatementsOfAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatementsOfAccount_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StatementsOfAccount_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StatementOfAccountLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatementOfAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PatientRegistrationId = table.Column<long>(type: "bigint", nullable: false),
                    RegistrationCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PatientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistrationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Net = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Paid = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementOfAccountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementOfAccountLines_StatementsOfAccount_StatementOfAccountId",
                        column: x => x.StatementOfAccountId,
                        principalTable: "StatementsOfAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatementOfAccountLines_StatementOfAccountId_Sequence",
                table: "StatementOfAccountLines",
                columns: new[] { "StatementOfAccountId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_StatementsOfAccount_CompanyId",
                table: "StatementsOfAccount",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementsOfAccount_CreatedAtUtc",
                table: "StatementsOfAccount",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StatementsOfAccount_PatientId",
                table: "StatementsOfAccount",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementsOfAccount_RegeneratedFromId",
                table: "StatementsOfAccount",
                column: "RegeneratedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementsOfAccount_StatementNumber",
                table: "StatementsOfAccount",
                column: "StatementNumber",
                unique: true);

            // Statement of Accounts (module 25) was print only. It now has Create (generate and regenerate) and Delete (void) as well. Users who
            // could print it keep working: they get both new permissions, and an administrator can take them away again in Users.
            migrationBuilder.Sql(@"
                UPDATE Modules SET HasCreate = 1, HasDelete = 1 WHERE Id = 25;
                UPDATE UserPermissions SET AllowCreate = 1, AllowDelete = 1 WHERE ModuleId = 25 AND AllowPrint = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE UserPermissions SET AllowCreate = 0, AllowDelete = 0 WHERE ModuleId = 25;
                UPDATE Modules SET HasCreate = 0, HasDelete = 0 WHERE Id = 25;
            ");

            migrationBuilder.DropTable(
                name: "StatementOfAccountLines");

            migrationBuilder.DropTable(
                name: "StatementsOfAccount");
        }
    }
}
