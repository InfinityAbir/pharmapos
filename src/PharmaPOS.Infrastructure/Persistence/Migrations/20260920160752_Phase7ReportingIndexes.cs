using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7ReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_sales_TenantId_CashierSubject",
                table: "sales",
                columns: new[] { "TenantId", "CashierSubject" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_payments_TenantId_Method",
                table: "sale_payments",
                columns: new[] { "TenantId", "Method" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_TenantId_Status_PurchaseDate",
                table: "purchases",
                columns: new[] { "TenantId", "Status", "PurchaseDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sales_TenantId_CashierSubject",
                table: "sales");

            migrationBuilder.DropIndex(
                name: "IX_sale_payments_TenantId_Method",
                table: "sale_payments");

            migrationBuilder.DropIndex(
                name: "IX_purchases_TenantId_Status_PurchaseDate",
                table: "purchases");
        }
    }
}
