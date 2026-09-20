using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Purchasing;
using PharmaPOS.Application.Features.Tenancy;
using PharmaPOS.Domain.Catalog;
using PharmaPOS.Domain.Identity;
using PharmaPOS.Domain.Inventory;
using PharmaPOS.Domain.Purchasing;
using PharmaPOS.Infrastructure.Identity.Tenancy;
using PharmaPOS.Infrastructure.Purchasing;

namespace PharmaPOS.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and (optionally) seeds the minimum data the platform needs to be
/// usable locally. Kept out of the request pipeline and idempotent so it is safe to run on every
/// controlled startup.
/// </summary>
public static class DatabaseInitializer
{
    public const string DemoTenantId = "demo-pharmacy";

    public const string DemoAdminEmail = "admin@pharmapos.local";
    public const string DemoOwnerEmail = "owner@demo-pharmacy.local";

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("PharmaPOS.Database");

        if (options.ApplyMigrationsOnStartup)
        {
            var store = provider.GetRequiredService<TenantStoreDbContext>();
            await store.Database.MigrateAsync(cancellationToken);

            var application = provider.GetRequiredService<AppDbContext>();
            await application.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("Database migrations applied.");
        }

        if (!options.SeedDemoData)
        {
            return;
        }

        var tenantStore = provider.GetRequiredService<TenantStoreDbContext>();
        var demo = await tenantStore.TenantInfo
            .FirstOrDefaultAsync(tenant => tenant.Id == DemoTenantId, cancellationToken);

        if (demo is null)
        {
            demo = new AppTenantInfo
            {
                Id = DemoTenantId,
                Identifier = DemoTenantId,
                Name = "Demo Pharmacy",
                Status = TenantStatus.Active,
            };

            tenantStore.TenantInfo.Add(demo);
            await tenantStore.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded demo tenant {TenantId}.", DemoTenantId);
        }

        var dbOptions = provider.GetRequiredService<DbContextOptions<AppDbContext>>();
        await using var db = MultiTenantDbContext.Create<AppDbContext, AppTenantInfo>(demo, dbOptions);

        await SeedPermissionsAsync(db, cancellationToken);
        await SeedDemoIdentityAsync(db, cancellationToken);
        await SeedDemoCatalogAsync(db, cancellationToken);
        await SeedDemoStockAsync(db, cancellationToken);
        await SeedDemoPurchasingAsync(provider, db, cancellationToken);

        logger.LogInformation(
            "Seeded demo identity, catalog, stock and purchasing data for tenant {TenantId}.",
            DemoTenantId);
    }

    private static async Task SeedPermissionsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        foreach (var descriptor in Permissions.All)
        {
            var exists = await db.Permissions
                .AnyAsync(permission => permission.Code == descriptor.Code, cancellationToken);

            if (exists)
            {
                continue;
            }

            db.Permissions.Add(new Permission
            {
                Code = descriptor.Code,
                Module = descriptor.Module,
                Description = descriptor.Description,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoIdentityAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var admin = await EnsureUserAsync(db, DemoAdminEmail, "Platform Administrator", cancellationToken);
        var owner = await EnsureUserAsync(db, DemoOwnerEmail, "Demo Pharmacy Owner", cancellationToken);

        // Tenant roles. Missing grants are added idempotently, so existing databases pick up
        // new permissions on upgrade. Cashier/Inventory Staff stay permission-less until the POS
        // and inventory phases land — no fake permissions are granted.
        var platformAdmin = await EnsureRoleAsync(
            db, "Platform Admin", "Full platform and tenant administration.", isSystem: true,
            [Permissions.Platform.TenantsManage, Permissions.Platform.UsersManage,
             Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage,
             Permissions.Tenant.RolesManage, Permissions.Tenant.MedicinesRead,
             Permissions.Tenant.MedicinesManage, Permissions.Tenant.PharmacyManage,
             Permissions.Tenant.InventoryRead, Permissions.Tenant.InventoryManage,
             Permissions.Tenant.PurchasingRead, Permissions.Tenant.PurchasingManage],
            cancellationToken);

        var ownerRole = await EnsureRoleAsync(
            db, "Owner", "Pharmacy owner: audit visibility and tenant administration.", isSystem: true,
            [Permissions.Platform.AuditRead, Permissions.Tenant.MembersManage,
             Permissions.Tenant.RolesManage, Permissions.Tenant.MedicinesRead,
             Permissions.Tenant.MedicinesManage, Permissions.Tenant.PharmacyManage,
             Permissions.Tenant.InventoryRead, Permissions.Tenant.InventoryManage,
             Permissions.Tenant.PurchasingRead, Permissions.Tenant.PurchasingManage],
            cancellationToken);

        await EnsureRoleAsync(db, "Manager", "Shift supervision, catalog, stock and purchase lookup.",
            isSystem: true, [Permissions.Tenant.MedicinesRead, Permissions.Tenant.InventoryRead,
                             Permissions.Tenant.PurchasingRead],
            cancellationToken);
        await EnsureRoleAsync(db, "Cashier", "Checkout operation (permissions arrive with the POS phase).",
            isSystem: true, [], cancellationToken);
        await EnsureRoleAsync(db, "Inventory Staff", "Stock receiving, adjustments and expiry handling.",
            isSystem: true, [Permissions.Tenant.MedicinesRead, Permissions.Tenant.InventoryRead,
                             Permissions.Tenant.InventoryManage],
            cancellationToken);

        await EnsureMembershipAsync(db, admin, platformAdmin, isDefault: true, cancellationToken);
        await EnsureMembershipAsync(db, owner, ownerRole, isDefault: true, cancellationToken);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        AppDbContext db,
        string email,
        string displayName,
        CancellationToken cancellationToken)
    {
        var normalized = email.ToLowerInvariant();

        var user = await db.ApplicationUsers
            .FirstOrDefaultAsync(candidate => candidate.Email == normalized, cancellationToken);

        if (user is not null)
        {
            return user;
        }

        user = new ApplicationUser
        {
            // KeycloakSubject stays null until the identity is provisioned/linked in Keycloak;
            // resolution falls back to email == Keycloak username for the demo accounts.
            KeycloakSubject = null,
            Email = normalized,
            DisplayName = displayName,
            IsActive = true,
        };

        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user;
    }

    private static async Task<Role> EnsureRoleAsync(
        AppDbContext db,
        string name,
        string? description,
        bool isSystem,
        string[] permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await db.Roles
            .Include(candidate => candidate.RolePermissions)
            .FirstOrDefaultAsync(candidate => candidate.Name == name, cancellationToken);

        if (role is null)
        {
            role = new Role
            {
                // Assigned explicitly: the tenant-composite alternate key must be non-null on Add.
                TenantId = TenantScope.CurrentTenantId(db),
                Name = name,
                Description = description,
                IsSystem = isSystem,
            };

            db.Roles.Add(role);
            await db.SaveChangesAsync(cancellationToken);
        }

        var granted = role.RolePermissions
            .Select(rolePermission => rolePermission.PermissionId)
            .ToHashSet();

        var permissionsByCode = await db.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code, cancellationToken);

        foreach (var code in permissionCodes.Distinct())
        {
            if (!permissionsByCode.TryGetValue(code, out var permission)
                || !granted.Add(permission.Id))
            {
                continue;
            }

            db.RolePermissions.Add(new RolePermission
            {
                TenantId = role.TenantId,
                RoleId = role.Id,
                PermissionId = permission.Id,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return role;
    }

    private static async Task EnsureMembershipAsync(
        AppDbContext db,
        ApplicationUser user,
        Role role,
        bool isDefault,
        CancellationToken cancellationToken)
    {
        var membership = await db.TenantMemberships
            .Include(candidate => candidate.MembershipRoles)
            .FirstOrDefaultAsync(
                candidate => candidate.ApplicationUserId == user.Id,
                cancellationToken);

        if (membership is null)
        {
            membership = new TenantMembership
            {
                // Assigned explicitly: the tenant-composite alternate key must be non-null on Add.
                TenantId = TenantScope.CurrentTenantId(db),
                ApplicationUserId = user.Id,
                Status = MembershipStatus.Active,
                IsDefault = isDefault,
            };

            db.TenantMemberships.Add(membership);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (membership.MembershipRoles.Any(assignment => assignment.RoleId == role.Id))
        {
            return;
        }

        db.MembershipRoles.Add(new MembershipRole
        {
            TenantId = membership.TenantId,
            MembershipId = membership.Id,
            RoleId = role.Id,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoCatalogAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var tenantId = TenantScope.CurrentTenantId(db);

        var analgesic = await EnsureCategoryAsync(db, tenantId, "Analgesic", "Pain relief medicines.", cancellationToken);
        var antibiotic = await EnsureCategoryAsync(db, tenantId, "Antibiotic", "Anti-bacterial medicines.", cancellationToken);
        var antacid = await EnsureCategoryAsync(db, tenantId, "Antacid", "Acidity and gastric medicines.", cancellationToken);
        var antihypertensive = await EnsureCategoryAsync(db, tenantId, "Antihypertensive", "Blood pressure medicines.", cancellationToken);

        var square = await EnsureManufacturerAsync(db, tenantId, "Square Pharmaceuticals", "Bangladesh", cancellationToken);
        var beximco = await EnsureManufacturerAsync(db, tenantId, "Beximco Pharmaceuticals", "Bangladesh", cancellationToken);
        var incepta = await EnsureManufacturerAsync(db, tenantId, "Incepta Pharmaceuticals", "Bangladesh", cancellationToken);

        await EnsureMedicineAsync(
            db, tenantId, brand: "Napa", generic: "Paracetamol", strength: "500mg",
            dosageForm: DosageForms.Tablet, manufacturer: square, category: analgesic,
            barcode: "8941154100017", unit: "strip", packSize: 10,
            purchasePrice: 8.00m, sellingPrice: 12.00m, reorderLevel: 50,
            vatRate: null, requiresPrescription: false, cancellationToken);
        await EnsureMedicineAsync(
            db, tenantId, brand: "Napa Extra", generic: "Paracetamol + Caffeine", strength: "500mg+65mg",
            dosageForm: DosageForms.Tablet, manufacturer: square, category: analgesic,
            barcode: "8941154100024", unit: "strip", packSize: 10,
            purchasePrice: 12.00m, sellingPrice: 18.00m, reorderLevel: 50,
            vatRate: null, requiresPrescription: false, cancellationToken);
        await EnsureMedicineAsync(
            db, tenantId, brand: "Fimoxyl", generic: "Amoxicillin", strength: "500mg",
            dosageForm: DosageForms.Capsule, manufacturer: square, category: antibiotic,
            barcode: "8941154100031", unit: "strip", packSize: 10,
            purchasePrice: 45.00m, sellingPrice: 60.00m, reorderLevel: 30,
            vatRate: null, requiresPrescription: true, cancellationToken);
        await EnsureMedicineAsync(
            db, tenantId, brand: "Seclo", generic: "Omeprazole", strength: "20mg",
            dosageForm: DosageForms.Capsule, manufacturer: square, category: antacid,
            barcode: "8941154100048", unit: "strip", packSize: 10,
            purchasePrice: 28.00m, sellingPrice: 40.00m, reorderLevel: 40,
            vatRate: null, requiresPrescription: false, cancellationToken);
        await EnsureMedicineAsync(
            db, tenantId, brand: "Amlocard", generic: "Amlodipine", strength: "5mg",
            dosageForm: DosageForms.Tablet, manufacturer: beximco, category: antihypertensive,
            barcode: "8941154200014", unit: "strip", packSize: 10,
            purchasePrice: 22.00m, sellingPrice: 32.00m, reorderLevel: 30,
            vatRate: null, requiresPrescription: true, cancellationToken);
        await EnsureMedicineAsync(
            db, tenantId, brand: "Neoceptin-R", generic: "Paracetamol", strength: "120mg/5ml",
            dosageForm: DosageForms.Syrup, manufacturer: incepta, category: analgesic,
            barcode: "8941154300011", unit: "bottle", packSize: null,
            purchasePrice: 35.00m, sellingPrice: 50.00m, reorderLevel: 20,
            vatRate: null, requiresPrescription: false, cancellationToken);

        await EnsurePharmacyProfileAsync(db, tenantId, cancellationToken);
    }

    private static async Task<MedicineCategory> EnsureCategoryAsync(
        AppDbContext db,
        string tenantId,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        var category = await db.MedicineCategories
            .FirstOrDefaultAsync(candidate => candidate.Name == name, cancellationToken);

        if (category is not null)
        {
            return category;
        }

        category = new MedicineCategory
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
        };

        db.MedicineCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return category;
    }

    private static async Task<Manufacturer> EnsureManufacturerAsync(
        AppDbContext db,
        string tenantId,
        string name,
        string? country,
        CancellationToken cancellationToken)
    {
        var manufacturer = await db.Manufacturers
            .FirstOrDefaultAsync(candidate => candidate.Name == name, cancellationToken);

        if (manufacturer is not null)
        {
            return manufacturer;
        }

        manufacturer = new Manufacturer
        {
            TenantId = tenantId,
            Name = name,
            Country = country,
        };

        db.Manufacturers.Add(manufacturer);
        await db.SaveChangesAsync(cancellationToken);

        return manufacturer;
    }

    private static async Task EnsureMedicineAsync(
        AppDbContext db,
        string tenantId,
        string brand,
        string generic,
        string strength,
        string dosageForm,
        Manufacturer manufacturer,
        MedicineCategory category,
        string barcode,
        string unit,
        int? packSize,
        decimal purchasePrice,
        decimal sellingPrice,
        int reorderLevel,
        decimal? vatRate,
        bool requiresPrescription,
        CancellationToken cancellationToken)
    {
        var exists = await db.Medicines
            .AnyAsync(candidate => candidate.Barcode == barcode, cancellationToken);

        if (exists)
        {
            return;
        }

        db.Medicines.Add(new Medicine
        {
            TenantId = tenantId,
            BrandName = brand,
            GenericName = generic,
            Strength = strength,
            DosageForm = dosageForm,
            ManufacturerId = manufacturer.Id,
            CategoryId = category.Id,
            Barcode = barcode,
            Unit = unit,
            PackSize = packSize,
            PurchasePrice = purchasePrice,
            SellingPrice = sellingPrice,
            ReorderLevel = reorderLevel,
            VatRate = vatRate,
            RequiresPrescription = requiresPrescription,
            IsActive = true,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoStockAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var tenantId = TenantScope.CurrentTenantId(db);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // Opening stock. Deliberately includes a near-expiry batch (Seclo SC-001), a low-stock
        // medicine (Amlocard: 10 on hand vs reorder 30) and a depleted expired batch (Fimoxyl
        // FM-000) so the alert lists have real demo content.
        await EnsureBatchAsync(db, tenantId, barcode: "8941154100017", batchNumber: "NP-001",
            quantity: 120, purchasePrice: 8.00m, sellingPrice: 12.00m,
            manufactureDate: new DateOnly(2026, 1, 10), expiryDate: new DateOnly(2027, 6, 30),
            supplierName: "Square Distributor", purchaseReference: "PO-2026-001", cancellationToken);
        await EnsureBatchAsync(db, tenantId, barcode: "8941154100024", batchNumber: "NE-001",
            quantity: 60, purchasePrice: 12.00m, sellingPrice: 18.00m,
            manufactureDate: new DateOnly(2026, 3, 5), expiryDate: new DateOnly(2028, 1, 31),
            supplierName: "Square Distributor", purchaseReference: "PO-2026-001", cancellationToken);
        await EnsureBatchAsync(db, tenantId, barcode: "8941154100031", batchNumber: "FM-001",
            quantity: 40, purchasePrice: 45.00m, sellingPrice: 60.00m,
            manufactureDate: new DateOnly(2026, 2, 1), expiryDate: new DateOnly(2027, 9, 30),
            supplierName: "Square Distributor", purchaseReference: "PO-2026-002", cancellationToken);
        await EnsureBatchAsync(db, tenantId, barcode: "8941154100048", batchNumber: "SC-002",
            quantity: 45, purchasePrice: 28.00m, sellingPrice: 40.00m,
            manufactureDate: new DateOnly(2026, 4, 12), expiryDate: new DateOnly(2027, 11, 30),
            supplierName: "Square Distributor", purchaseReference: "PO-2026-002", cancellationToken);
        await EnsureBatchAsync(db, tenantId, barcode: "8941154100048", batchNumber: "SC-001",
            quantity: 12, purchasePrice: 28.00m, sellingPrice: 40.00m,
            manufactureDate: new DateOnly(2025, 10, 1), expiryDate: today.AddDays(18),
            supplierName: "Square Distributor", purchaseReference: "PO-2025-114", cancellationToken);
        await EnsureBatchAsync(db, tenantId, barcode: "8941154200014", batchNumber: "AM-001",
            quantity: 10, purchasePrice: 22.00m, sellingPrice: 32.00m,
            manufactureDate: new DateOnly(2026, 5, 20), expiryDate: new DateOnly(2027, 5, 31),
            supplierName: "Beximco Depot", purchaseReference: "PO-2026-003", cancellationToken);
        await EnsureBatchAsync(db, tenantId, barcode: "8941154300011", batchNumber: "NR-001",
            quantity: 25, purchasePrice: 35.00m, sellingPrice: 50.00m,
            manufactureDate: new DateOnly(2026, 6, 15), expiryDate: new DateOnly(2027, 4, 30),
            supplierName: "Incepta Depot", purchaseReference: "PO-2026-004", cancellationToken);

        await EnsureExpiredBatchAsync(db, tenantId, barcode: "8941154100031", batchNumber: "FM-000",
            removedQuantity: 8, purchasePrice: 45.00m, sellingPrice: 60.00m,
            manufactureDate: new DateOnly(2024, 6, 1), expiryDate: today.AddDays(-90),
            supplierName: "Square Distributor", purchaseReference: "PO-2024-088", cancellationToken);
    }

    private static async Task EnsureBatchAsync(
        AppDbContext db,
        string tenantId,
        string barcode,
        string batchNumber,
        int quantity,
        decimal purchasePrice,
        decimal sellingPrice,
        DateOnly? manufactureDate,
        DateOnly expiryDate,
        string? supplierName,
        string? purchaseReference,
        CancellationToken cancellationToken)
    {
        var medicine = await db.Medicines
            .FirstOrDefaultAsync(candidate => candidate.Barcode == barcode, cancellationToken);

        if (medicine is null)
        {
            return;
        }

        var exists = await db.MedicineBatches.AnyAsync(
            candidate => candidate.MedicineId == medicine.Id && candidate.BatchNumber == batchNumber,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var batch = new MedicineBatch
        {
            TenantId = tenantId,
            MedicineId = medicine.Id,
            BatchNumber = batchNumber,
            PurchasePrice = purchasePrice,
            SellingPrice = sellingPrice,
            Quantity = quantity,
            ManufactureDate = manufactureDate,
            ExpiryDate = expiryDate,
            SupplierName = supplierName,
            PurchaseReference = purchaseReference,
        };

        db.MedicineBatches.Add(batch);
        db.InventoryMovements.Add(new InventoryMovement
        {
            TenantId = tenantId,
            MedicineId = medicine.Id,
            BatchId = batch.Id,
            Type = MovementTypes.OpeningStock,
            QuantityChange = quantity,
            QuantityAfter = quantity,
            Reason = purchaseReference,
            ActorSubject = null,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureExpiredBatchAsync(
        AppDbContext db,
        string tenantId,
        string barcode,
        string batchNumber,
        int removedQuantity,
        decimal purchasePrice,
        decimal sellingPrice,
        DateOnly? manufactureDate,
        DateOnly expiryDate,
        string? supplierName,
        string? purchaseReference,
        CancellationToken cancellationToken)
    {
        var medicine = await db.Medicines
            .FirstOrDefaultAsync(candidate => candidate.Barcode == barcode, cancellationToken);

        if (medicine is null)
        {
            return;
        }

        var exists = await db.MedicineBatches.AnyAsync(
            candidate => candidate.MedicineId == medicine.Id && candidate.BatchNumber == batchNumber,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var batch = new MedicineBatch
        {
            TenantId = tenantId,
            MedicineId = medicine.Id,
            BatchNumber = batchNumber,
            PurchasePrice = purchasePrice,
            SellingPrice = sellingPrice,
            Quantity = 0,
            ManufactureDate = manufactureDate,
            ExpiryDate = expiryDate,
            SupplierName = supplierName,
            PurchaseReference = purchaseReference,
        };

        db.MedicineBatches.Add(batch);
        db.InventoryMovements.Add(new InventoryMovement
        {
            TenantId = tenantId,
            MedicineId = medicine.Id,
            BatchId = batch.Id,
            Type = MovementTypes.Expired,
            QuantityChange = -removedQuantity,
            QuantityAfter = 0,
            Reason = null,
            ActorSubject = null,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoPurchasingAsync(
        IServiceProvider provider,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        // Repositories are constructed over the tenant-bound seed context directly: the DI
        // scope at startup has no HTTP tenant, so the injected AppDbContext would be unbound.
        // Actor attribution is null here (no principal); deliveries are attributed on real use.
        var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        var suppliers = new SupplierRepository(db);
        var purchases = new PurchaseRepository(db, httpContextAccessor);

        await EnsureSupplierAsync(db, "Square Distributor", "Square Pharmaceuticals Ltd.",
            "+880-2-1111111", "sales@square-dist.local", "Dhaka, Bangladesh",
            "Distribution Manager", "Net 30", cancellationToken);
        await EnsureSupplierAsync(db, "Beximco Depot", "Beximco Pharmaceuticals Ltd.",
            "+880-2-2222222", "sales@beximco-depot.local", "Dhaka, Bangladesh",
            "Depot In-charge", "Net 15", cancellationToken);
        await EnsureSupplierAsync(db, "Incepta Depot", "Incepta Pharmaceuticals Ltd.",
            "+880-2-3333333", null, "Dhaka, Bangladesh",
            null, "Cash on delivery", cancellationToken);

        await EnsureDemoPurchaseAsync(db, suppliers, purchases, cancellationToken);
    }

    private static async Task EnsureSupplierAsync(
        AppDbContext db,
        string name,
        string? company,
        string? phone,
        string? email,
        string? address,
        string? contactPerson,
        string? paymentTerms,
        CancellationToken cancellationToken)
    {
        var exists = await db.Suppliers
            .AnyAsync(candidate => candidate.Name == name, cancellationToken);

        if (exists)
        {
            return;
        }

        db.Suppliers.Add(new Supplier
        {
            TenantId = TenantScope.CurrentTenantId(db),
            Name = name,
            Company = company,
            Phone = phone,
            Email = email,
            Address = address,
            ContactPerson = contactPerson,
            PaymentTerms = paymentTerms,
            IsActive = true,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDemoPurchaseAsync(
        AppDbContext db,
        ISupplierRepository suppliers,
        IPurchaseRepository purchases,
        CancellationToken cancellationToken)
    {
        const string idempotencyKey = "seed-demo-po-001";

        // Already seeded (replay check doubles as the existence check).
        var existing = await db.Purchases
            .AnyAsync(candidate => candidate.SupplierInvoiceNumber == "SQ-2026-005",
                cancellationToken);

        if (existing)
        {
            return;
        }

        var supplier = (await suppliers.ListAsync("Square Distributor", cancellationToken))
            .FirstOrDefault(candidate => candidate.Name == "Square Distributor");

        var napa = await db.Medicines
            .FirstOrDefaultAsync(candidate => candidate.Barcode == "8941154100017",
                cancellationToken);

        if (supplier is null || napa is null)
        {
            return;
        }

        var draft = await purchases.CreateDraftAsync(
            new PurchaseInput(
                SupplierId: supplier.Id,
                PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow.Date),
                SupplierInvoiceNumber: "SQ-2026-005",
                DiscountAmount: 0,
                TaxAmount: 0,
                Notes: "Seeded demo purchase.",
                Items:
                [
                    new PurchaseItemInput(
                        MedicineId: napa.Id,
                        BatchNumber: "NP-002",
                        ManufactureDate: new DateOnly(2026, 7, 1),
                        ExpiryDate: new DateOnly(2028, 6, 30),
                        Quantity: 30,
                        UnitPrice: 8.00m,
                        DiscountAmount: 0,
                        SellingPrice: null),
                ]),
            cancellationToken);

        await purchases.ReceiveAsync(draft.Id, idempotencyKey, cancellationToken);
    }

    private static async Task EnsurePharmacyProfileAsync(
        AppDbContext db,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var exists = await db.PharmacyProfiles.AnyAsync(cancellationToken);
        if (exists)
        {
            return;
        }

        db.PharmacyProfiles.Add(new PharmacyProfile
        {
            TenantId = tenantId,
            PharmacyName = "Demo Pharmacy",
            Address = "House 12, Road 5, Dhanmondi, Dhaka 1205",
            Phone = "+880-2-0000000",
            Email = "info@demo-pharmacy.local",
            Website = null,
            RegistrationInfo = "DGDA License DEMO-0001",
            VatInfo = "BIN 000000000-0000",
            InvoiceFooter = "Thank you for shopping with Demo Pharmacy.",
            Currency = "BDT",
            DefaultLanguage = "en",
            Timezone = "Asia/Dhaka",
            LogoUrl = null,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}