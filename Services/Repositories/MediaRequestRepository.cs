using Microsoft.EntityFrameworkCore;
using MoviesMafia.Data;
using MoviesMafia.Domain.Entities;

namespace MoviesMafia.Services.Repositories;

public sealed class MediaRequestRepository : Repository<MediaRequest>, IMediaRequestRepository
{
    public MediaRequestRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<MediaRequest>> GetByUserAsync(string userId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
}
