using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiagnosticLabs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscountId",
                table: "PatientRegistrations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientRegistrations_DiscountId",
                table: "PatientRegistrations",
                column: "DiscountId");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientRegistrations_Discounts_DiscountId",
                table: "PatientRegistrations",
                column: "DiscountId",
                principalTable: "Discounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientRegistrations_Discounts_DiscountId",
                table: "PatientRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_PatientRegistrations_DiscountId",
                table: "PatientRegistrations");

            migrationBuilder.DropColumn(
                name: "DiscountId",
                table: "PatientRegistrations");
        }
    }
}
