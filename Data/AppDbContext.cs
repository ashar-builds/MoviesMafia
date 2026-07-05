using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MoviesMafia.Domain.Entities;

namespace MoviesMafia.Data;

// Implements IDataProtectionKeyContext so the Data Protection key ring is stored in Postgres.
// Keys then survive app restarts and are shared across all instances — without this, keys land
// on the container's ephemeral filesystem, regenerate on every restart, and log everyone out
// (auth cookie + ReactiveBlazor state tokens are both DP-encrypted).
public class AppDbContext : IdentityDbContext<AppUser>, IDataProtectionKeyContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<MediaRequest> MediaRequests => Set<MediaRequest>();

    /// <summary>Data Protection key ring, persisted in the database.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAudit()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IBaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
                entry.Entity.CreatedBy = userId;
                entry.Entity.UpdatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userId;
            }
        }
    }
}
