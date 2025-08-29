using System.ComponentModel.DataAnnotations;

namespace ErpApi;

public class InventoryItem
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public int QuantityOnHand { get; set; }

    public decimal UnitPrice { get; set; }

    public int? ReorderLevel { get; set; }

    public int CompanyId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? UpdatedUtc { get; set; }
}