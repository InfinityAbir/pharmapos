using MediatR;
using Microsoft.AspNetCore.Mvc;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Application.Common.Authorization;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Application.Features.Catalog;
using PharmaPOS.Application.Features.Catalog.CreateCategory;
using PharmaPOS.Application.Features.Catalog.CreateManufacturer;
using PharmaPOS.Application.Features.Catalog.CreateMedicine;
using PharmaPOS.Application.Features.Catalog.DeleteCategory;
using PharmaPOS.Application.Features.Catalog.DeleteManufacturer;
using PharmaPOS.Application.Features.Catalog.GetCatalogSummary;
using PharmaPOS.Application.Features.Catalog.GetMedicine;
using PharmaPOS.Application.Features.Catalog.GetMedicineByBarcode;
using PharmaPOS.Application.Features.Catalog.GetPharmacyProfile;
using PharmaPOS.Application.Features.Catalog.ListCategories;
using PharmaPOS.Application.Features.Catalog.ListManufacturers;
using PharmaPOS.Application.Features.Catalog.SearchMedicines;
using PharmaPOS.Application.Features.Catalog.SetMedicineStatus;
using PharmaPOS.Application.Features.Catalog.UpdateCategory;
using PharmaPOS.Application.Features.Catalog.UpdateManufacturer;
using PharmaPOS.Application.Features.Catalog.UpdateMedicine;
using PharmaPOS.Application.Features.Catalog.UpsertPharmacyProfile;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// Current-tenant pharmacy catalog: medicines, categories, manufacturers and the pharmacy profile.
/// Everything here is scoped to the active tenant by the tenant-filtered context.
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/tenant")]
public sealed class CatalogController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogController(ISender sender) => _sender = sender;

    /// <summary>Returns catalog totals for dashboards and badges.</summary>
    [HasPermission(Permissions.Tenant.MedicinesRead)]
    [HttpGet("catalog/summary")]
    [ProducesResponseType(typeof(CatalogSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CatalogSummaryDto>> GetCatalogSummary(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCatalogSummaryQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Searches the medicine catalog by brand, generic, barcode or manufacturer name, with
    /// optional category/manufacturer/status filters and paging.
    /// </summary>
    [HasPermission(Permissions.Tenant.MedicinesRead)]
    [HttpGet("medicines")]
    [ProducesResponseType(typeof(IReadOnlyList<MedicineDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MedicineDto>>> SearchMedicines(
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? manufacturerId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 25)
    {
        var result = await _sender.Send(
            new SearchMedicinesQuery(search, categoryId, manufacturerId, isActive, skip, take),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Gets a single medicine by id.</summary>
    [HasPermission(Permissions.Tenant.MedicinesRead)]
    [HttpGet("medicines/{id:guid}")]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MedicineDto>> GetMedicine(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMedicineQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Resolves a medicine by exact barcode (scanner lookup).</summary>
    [HasPermission(Permissions.Tenant.MedicinesRead)]
    [HttpGet("medicines/by-barcode/{barcode}")]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MedicineDto>> GetMedicineByBarcode(
        [FromRoute] string barcode,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMedicineByBarcodeQuery(barcode), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a medicine. Barcodes must be unique within the tenant.</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpPost("medicines")]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<MedicineDto>> CreateMedicine(
        [FromBody] MedicineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateMedicineCommand(request.ToInput()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a medicine (emits a price-change audit event when prices differ).</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpPut("medicines/{id:guid}")]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MedicineDto>> UpdateMedicine(
        [FromRoute] Guid id,
        [FromBody] MedicineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateMedicineCommand(id, request.ToInput()),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Activates or deactivates a medicine (medicines are never hard-deleted).</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpPatch("medicines/{id:guid}/status")]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MedicineDto>> SetMedicineStatus(
        [FromRoute] Guid id,
        [FromBody] SetMedicineStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SetMedicineStatusCommand(id, request.IsActive),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Lists the tenant's medicine categories.</summary>
    [HasPermission(Permissions.Tenant.MedicinesRead)]
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> ListCategories(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListCategoriesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a medicine category.</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpPost("categories")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        [FromBody] CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateCategoryCommand(request.Name, request.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Renames a medicine category.</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpPut("categories/{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(
        [FromRoute] Guid id,
        [FromBody] CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateCategoryCommand(id, request.Name, request.Description),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Deletes a category (refused while medicines reference it).</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpDelete("categories/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteCategory(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteCategoryCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Lists the tenant's manufacturers.</summary>
    [HasPermission(Permissions.Tenant.MedicinesRead)]
    [HttpGet("manufacturers")]
    [ProducesResponseType(typeof(IReadOnlyList<ManufacturerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ManufacturerDto>>> ListManufacturers(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ListManufacturersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a manufacturer.</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpPost("manufacturers")]
    [ProducesResponseType(typeof(ManufacturerDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ManufacturerDto>> CreateManufacturer(
        [FromBody] ManufacturerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateManufacturerCommand(request.Name, request.Country),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a manufacturer.</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpPut("manufacturers/{id:guid}")]
    [ProducesResponseType(typeof(ManufacturerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ManufacturerDto>> UpdateManufacturer(
        [FromRoute] Guid id,
        [FromBody] ManufacturerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateManufacturerCommand(id, request.Name, request.Country),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Deletes a manufacturer (refused while medicines reference it).</summary>
    [HasPermission(Permissions.Tenant.MedicinesManage)]
    [HttpDelete("manufacturers/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteManufacturer(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new DeleteManufacturerCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Gets the tenant's pharmacy profile (404 until configured).</summary>
    [HasPermission(Permissions.Tenant.MedicinesRead)]
    [HttpGet("pharmacy-profile")]
    [ProducesResponseType(typeof(PharmacyProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PharmacyProfileDto>> GetPharmacyProfile(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPharmacyProfileQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates or replaces the tenant's pharmacy profile.</summary>
    [HasPermission(Permissions.Tenant.PharmacyManage)]
    [HttpPut("pharmacy-profile")]
    [ProducesResponseType(typeof(PharmacyProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PharmacyProfileDto>> UpsertPharmacyProfile(
        [FromBody] PharmacyProfileDto profile,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpsertPharmacyProfileCommand(profile),
            cancellationToken);

        return Ok(result);
    }
}

public sealed record MedicineRequest(
    string BrandName,
    string GenericName,
    string Strength,
    string DosageForm,
    Guid? ManufacturerId,
    Guid? CategoryId,
    string? Barcode,
    string Unit,
    int? PackSize,
    decimal PurchasePrice,
    decimal SellingPrice,
    int ReorderLevel,
    decimal? VatRate,
    bool RequiresPrescription)
{
    public MedicineInput ToInput() => new(
        BrandName, GenericName, Strength, DosageForm, ManufacturerId, CategoryId,
        Barcode, Unit, PackSize, PurchasePrice, SellingPrice, ReorderLevel,
        VatRate, RequiresPrescription);
}

public sealed record SetMedicineStatusRequest(bool IsActive);

public sealed record CategoryRequest(string Name, string? Description);

public sealed record ManufacturerRequest(string Name, string? Country);
