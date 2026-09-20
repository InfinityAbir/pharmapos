using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_counters",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_counters", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PrescriptionReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CashierSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales", x => x.Id);
                    table.UniqueConstraint("AK_sales_Id_TenantId", x => new { x.Id, x.TenantId });
                });

            migrationBuilder.CreateTable(
                name: "sale_idempotency_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_idempotency_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_idempotency_keys_sales_SaleId_TenantId",
                        columns: x => new { x.SaleId, x.TenantId },
                        principalTable: "sales",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_items_medicine_batches_BatchId_TenantId",
                        columns: x => new { x.BatchId, x.TenantId },
                        principalTable: "medicine_batches",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_items_medicines_MedicineId_TenantId",
                        columns: x => new { x.MedicineId, x.TenantId },
                        principalTable: "medicines",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_items_sales_SaleId_TenantId",
                        columns: x => new { x.SaleId, x.TenantId },
                        principalTable: "sales",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReceivedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_payments_sales_SaleId_TenantId",
                        columns: x => new { x.SaleId, x.TenantId },
                        principalTable: "sales",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sale_idempotency_keys_SaleId_TenantId",
                table: "sale_idempotency_keys",
                columns: new[] { "SaleId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_idempotency_keys_TenantId_Key",
                table: "sale_idempotency_keys",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_BatchId_TenantId",
                table: "sale_items",
                columns: new[] { "BatchId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_MedicineId_TenantId",
                table: "sale_items",
                columns: new[] { "MedicineId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_SaleId_TenantId",
                table: "sale_items",
                columns: new[] { "SaleId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_TenantId_BatchId",
                table: "sale_items",
                columns: new[] { "TenantId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_TenantId_MedicineId",
                table: "sale_items",
                columns: new[] { "TenantId", "MedicineId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_items_TenantId_SaleId",
                table: "sale_items",
                columns: new[] { "TenantId", "SaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_payments_SaleId_TenantId",
                table: "sale_payments",
                columns: new[] { "SaleId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_payments_TenantId_SaleId",
                table: "sale_payments",
                columns: new[] { "TenantId", "SaleId" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_TenantId_InvoiceNumber",
                table: "sales",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_TenantId_OccurredAtUtc",
                table: "sales",
                columns: new[] { "TenantId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_counters");

            migrationBuilder.DropTable(
                name: "sale_idempotency_keys");

            migrationBuilder.DropTable(
                name: "sale_items");

            migrationBuilder.DropTable(
                name: "sale_payments");

            migrationBuilder.DropTable(
                name: "sales");
        }
    }
}
