using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_medicines_Id_TenantId",
                table: "medicines",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateTable(
                name: "medicine_batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PurchaseReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicine_batches", x => x.Id);
                    table.UniqueConstraint("AK_medicine_batches_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_medicine_batches_medicines_MedicineId_TenantId",
                        columns: x => new { x.MedicineId, x.TenantId },
                        principalTable: "medicines",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MedicineId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuantityChange = table.Column<int>(type: "integer", nullable: false),
                    QuantityAfter = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ActorSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_movements_medicine_batches_BatchId_TenantId",
                        columns: x => new { x.BatchId, x.TenantId },
                        principalTable: "medicine_batches",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_medicines_MedicineId_TenantId",
                        columns: x => new { x.MedicineId, x.TenantId },
                        principalTable: "medicines",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_BatchId_TenantId",
                table: "inventory_movements",
                columns: new[] { "BatchId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_MedicineId_TenantId",
                table: "inventory_movements",
                columns: new[] { "MedicineId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_TenantId_BatchId",
                table: "inventory_movements",
                columns: new[] { "TenantId", "BatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_TenantId_MedicineId",
                table: "inventory_movements",
                columns: new[] { "TenantId", "MedicineId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_TenantId_OccurredAtUtc",
                table: "inventory_movements",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_medicine_batches_MedicineId_TenantId",
                table: "medicine_batches",
                columns: new[] { "MedicineId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_medicine_batches_TenantId_ExpiryDate",
                table: "medicine_batches",
                columns: new[] { "TenantId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_medicine_batches_TenantId_MedicineId",
                table: "medicine_batches",
                columns: new[] { "TenantId", "MedicineId" });

            migrationBuilder.CreateIndex(
                name: "IX_medicine_batches_TenantId_MedicineId_BatchNumber",
                table: "medicine_batches",
                columns: new[] { "TenantId", "MedicineId", "BatchNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "medicine_batches");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_medicines_Id_TenantId",
                table: "medicines");
        }
    }
}
