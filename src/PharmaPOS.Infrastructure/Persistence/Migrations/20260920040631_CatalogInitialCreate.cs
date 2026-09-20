using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaPOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manufacturers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Country = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manufacturers", x => x.Id);
                    table.UniqueConstraint("AK_manufacturers_Id_TenantId", x => new { x.Id, x.TenantId });
                });

            migrationBuilder.CreateTable(
                name: "medicine_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicine_categories", x => x.Id);
                    table.UniqueConstraint("AK_medicine_categories_Id_TenantId", x => new { x.Id, x.TenantId });
                });

            migrationBuilder.CreateTable(
                name: "pharmacy_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PharmacyName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Website = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RegistrationInfo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    VatInfo = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InvoiceFooter = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pharmacy_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "medicines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    GenericName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Strength = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DosageForm = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ManufacturerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Barcode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PackSize = table.Column<int>(type: "integer", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReorderLevel = table.Column<int>(type: "integer", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    RequiresPrescription = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medicines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_medicines_manufacturers_ManufacturerId_TenantId",
                        columns: x => new { x.ManufacturerId, x.TenantId },
                        principalTable: "manufacturers",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medicines_medicine_categories_CategoryId_TenantId",
                        columns: x => new { x.CategoryId, x.TenantId },
                        principalTable: "medicine_categories",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manufacturers_TenantId_Name",
                table: "manufacturers",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medicine_categories_TenantId_Name",
                table: "medicine_categories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medicines_CategoryId_TenantId",
                table: "medicines",
                columns: new[] { "CategoryId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_medicines_ManufacturerId_TenantId",
                table: "medicines",
                columns: new[] { "ManufacturerId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_medicines_TenantId_Barcode",
                table: "medicines",
                columns: new[] { "TenantId", "Barcode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medicines_TenantId_BrandName",
                table: "medicines",
                columns: new[] { "TenantId", "BrandName" });

            migrationBuilder.CreateIndex(
                name: "IX_medicines_TenantId_GenericName",
                table: "medicines",
                columns: new[] { "TenantId", "GenericName" });

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_profiles_TenantId",
                table: "pharmacy_profiles",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medicines");

            migrationBuilder.DropTable(
                name: "pharmacy_profiles");

            migrationBuilder.DropTable(
                name: "manufacturers");

            migrationBuilder.DropTable(
                name: "medicine_categories");
        }
    }
}
