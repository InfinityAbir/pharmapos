using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6CustomersReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_sale_items_Id_TenantId",
                table: "sale_items",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.UniqueConstraint("AK_customers_Id_TenantId", x => new { x.Id, x.TenantId });
                });

            migrationBuilder.CreateTable(
                name: "sale_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Restock = table.Column<bool>(type: "boolean", nullable: false),
                    RefundMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RefundReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_returns", x => x.Id);
                    table.UniqueConstraint("AK_sale_returns_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_sale_returns_sales_SaleId_TenantId",
                        columns: x => new { x.SaleId, x.TenantId },
                        principalTable: "sales",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_return_idempotency_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_return_idempotency_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_return_idempotency_keys_sale_returns_SaleReturnId_Tena~",
                        columns: x => new { x.SaleReturnId, x.TenantId },
                        principalTable: "sale_returns",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_return_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SaleReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_return_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_return_items_medicine_batches_BatchId_TenantId",
                        columns: x => new { x.BatchId, x.TenantId },
                        principalTable: "medicine_batches",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_return_items_medicines_MedicineId_TenantId",
                        columns: x => new { x.MedicineId, x.TenantId },
                        principalTable: "medicines",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_return_items_sale_items_SaleItemId_TenantId",
                        columns: x => new { x.SaleItemId, x.TenantId },
                        principalTable: "sale_items",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_return_items_sale_returns_SaleReturnId_TenantId",
                        columns: x => new { x.SaleReturnId, x.TenantId },
                        principalTable: "sale_returns",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_TenantId_CustomerId",
                table: "sales",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_IsActive",
                table: "customers",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Name",
                table: "customers",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Phone",
                table: "customers",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_idempotency_keys_SaleReturnId_TenantId",
                table: "sale_return_idempotency_keys",
                columns: new[] { "SaleReturnId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_idempotency_keys_TenantId_Key",
                table: "sale_return_idempotency_keys",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_items_BatchId_TenantId",
                table: "sale_return_items",
                columns: new[] { "BatchId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_items_MedicineId_TenantId",
                table: "sale_return_items",
                columns: new[] { "MedicineId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_items_SaleItemId_TenantId",
                table: "sale_return_items",
                columns: new[] { "SaleItemId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_items_SaleReturnId_TenantId",
                table: "sale_return_items",
                columns: new[] { "SaleReturnId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_items_TenantId_SaleItemId",
                table: "sale_return_items",
                columns: new[] { "TenantId", "SaleItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_items_TenantId_SaleReturnId",
                table: "sale_return_items",
                columns: new[] { "TenantId", "SaleReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_returns_SaleId_TenantId",
                table: "sale_returns",
                columns: new[] { "SaleId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_returns_TenantId_OccurredAtUtc",
                table: "sale_returns",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_returns_TenantId_SaleId",
                table: "sale_returns",
                columns: new[] { "TenantId", "SaleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "sale_return_idempotency_keys");

            migrationBuilder.DropTable(
                name: "sale_return_items");

            migrationBuilder.DropTable(
                name: "sale_returns");

            migrationBuilder.DropIndex(
                name: "IX_sales_TenantId_CustomerId",
                table: "sales");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_sale_items_Id_TenantId",
                table: "sale_items");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "sales");
        }
    }
}
