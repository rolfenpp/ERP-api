using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using Recipt_api;

[ApiController]
[Route("inventory")]
[Authorize] // require JWT for all
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public InventoryController(ApplicationDbContext db) => _db = db;

    private int GetCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    // List inventory (user or admin via permission)
    [HttpGet]
    [Authorize(Policy = Permissions.ViewInventory)]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetAll()
    {
        var companyId = GetCompanyId();

        var items = await _db.InventoryItems
            .Where(i => i.CompanyId == companyId)
            .OrderBy(i => i.Name)
            .Select(i => new InventoryItemDto
            {
                Id = i.Id,
                Sku = i.Sku,
                Name = i.Name,
                Description = i.Description,
                Category = i.Category,
                QuantityOnHand = i.QuantityOnHand,
                UnitPrice = i.UnitPrice,
                ReorderLevel = i.ReorderLevel,
                CreatedUtc = i.CreatedUtc,
                UpdatedUtc = i.UpdatedUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    // Get single item (user or admin via permission)
    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.ViewInventory)]
    public async Task<ActionResult<InventoryItemDto>> GetById(int id)
    {
        var companyId = GetCompanyId();

        var item = await _db.InventoryItems
            .Where(i => i.Id == id && i.CompanyId == companyId)
            .Select(i => new InventoryItemDto
            {
                Id = i.Id,
                Sku = i.Sku,
                Name = i.Name,
                Description = i.Description,
                Category = i.Category,
                QuantityOnHand = i.QuantityOnHand,
                UnitPrice = i.UnitPrice,
                ReorderLevel = i.ReorderLevel,
                CreatedUtc = i.CreatedUtc,
                UpdatedUtc = i.UpdatedUtc
            })
            .FirstOrDefaultAsync();

        if (item is null) return NotFound();
        return Ok(item);
    }

    // Create item (user or admin via permission)
    [HttpPost]
    [Authorize(Policy = Permissions.CreateInventory)]
    public async Task<ActionResult<InventoryItemDto>> Create([FromBody] CreateInventoryItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var companyId = GetCompanyId();
        var sku = (dto.Sku ?? string.Empty).Trim();

        if (!string.IsNullOrEmpty(sku))
        {
            var exists = await _db.InventoryItems.AnyAsync(i => i.CompanyId == companyId && i.Sku == sku);
            if (exists) return Conflict("An item with this SKU already exists in your company.");
        }

        var entity = new InventoryItem
        {
            Sku = sku,
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Category = dto.Category?.Trim(),
            QuantityOnHand = dto.QuantityOnHand, // 0 is valid
            UnitPrice = dto.UnitPrice,
            ReorderLevel = dto.ReorderLevel,
            CompanyId = companyId,
            CreatedUtc = DateTime.UtcNow
        };

        _db.InventoryItems.Add(entity);
        await _db.SaveChangesAsync();

        var result = new InventoryItemDto
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            QuantityOnHand = entity.QuantityOnHand,
            UnitPrice = entity.UnitPrice,
            ReorderLevel = entity.ReorderLevel,
            CreatedUtc = entity.CreatedUtc,
            UpdatedUtc = entity.UpdatedUtc
        };

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, result);
    }

    // Update item (user or admin via permission)
    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.EditInventory)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInventoryItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var companyId = GetCompanyId();

        var entity = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId);
        if (entity is null) return NotFound();

        if (dto.Sku is not null) // only process SKU if provided
        {
            var newSku = dto.Sku.Trim();
            if (!string.Equals(entity.Sku, newSku, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(newSku))
                {
                    var skuTaken = await _db.InventoryItems.AnyAsync(i => i.CompanyId == companyId && i.Sku == newSku && i.Id != id);
                    if (skuTaken) return Conflict("An item with this SKU already exists in your company.");
                }
                entity.Sku = newSku; // allow clearing to empty
            }
        }

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.Category = dto.Category?.Trim();
        entity.QuantityOnHand = dto.QuantityOnHand;
        entity.UnitPrice = dto.UnitPrice;
        entity.ReorderLevel = dto.ReorderLevel;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Delete item (user or admin via permission)
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.DeleteInventory)]
    public async Task<IActionResult> Delete(int id)
    {
        var companyId = GetCompanyId();

        var entity = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId);
        if (entity is null) return NotFound();

        _db.InventoryItems.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class InventoryItemDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int QuantityOnHand { get; set; }
    public decimal UnitPrice { get; set; }
    public int? ReorderLevel { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UpdatedUtc { get; set; }
}

public class CreateInventoryItemDto
{
    [MaxLength(64)]
    public string? Sku { get; set; } // optional

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; } // optional

    [Range(0, int.MaxValue)]
    public int QuantityOnHand { get; set; } // required conceptually; 0 is valid

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int? ReorderLevel { get; set; }
}

public class UpdateInventoryItemDto
{
    [MaxLength(64)]
    public string? Sku { get; set; } // optional on update too

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; } // optional

    [Range(0, int.MaxValue)]
    public int QuantityOnHand { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int? ReorderLevel { get; set; }
}