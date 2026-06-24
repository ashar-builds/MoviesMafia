using Microsoft.EntityFrameworkCore;
using MoviesMafia.Data;

namespace MoviesMafia.Services.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRepository{T}"/>.
///
/// Owns a single <see cref="AppDbContext"/> created from <see cref="IDbContextFactory{TContext}"/>
/// rather than sharing the request-scoped context. Under Blazor Static SSR / ReactiveBlazor,
/// several components on one page (and out-of-band signal re-renders) run concurrently and would
/// otherwise issue overlapping operations on a single shared context — which EF Core forbids
/// ("A second operation was started on this context instance..."). Each repository instance is
/// registered transient, so every consuming component gets its own isolated context.
/// </summary>
public class Repository<T> : IRepository<T>, IAsyncDisposable where T : class
{
    protected readonly AppDbContext Db;
    protected readonly DbSet<T> Set;

    public Repository(IDbContextFactory<AppDbContext> dbFactory)
    {
        Db = dbFactory.CreateDbContext();
        Set = Db.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
