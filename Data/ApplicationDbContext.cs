using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ErpApi;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantProvider? _tenantProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantProvider? tenantProvider = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Foreign keys and indexes for tenant integrity
        builder.Entity<ApplicationUser>()
            .HasIndex(u => u.CompanyId);

        builder.Entity<Company>()
            .HasIndex(c => c.Name)
            .IsUnique(false);

        builder.Entity<Project>()
            .HasIndex(p => p.CompanyId);

        builder.Entity<InventoryItem>()
            .HasIndex(i => i.CompanyId);

        // Global query filters for tenant isolation
        // Do NOT filter ApplicationUser globally to avoid breaking login and admin flows.
        var companyId = _tenantProvider?.CompanyId ?? 0;

        builder.Entity<Project>().HasQueryFilter(p => companyId == 0 || p.CompanyId == companyId);
        builder.Entity<InventoryItem>().HasQueryFilter(i => companyId == 0 || i.CompanyId == companyId);
    }
}