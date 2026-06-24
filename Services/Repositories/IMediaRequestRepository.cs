using MoviesMafia.Domain.Entities;

namespace MoviesMafia.Services.Repositories;

public interface IMediaRequestRepository : IRepository<MediaRequest>
{
    Task<IReadOnlyList<MediaRequest>> GetByUserAsync(string userId, CancellationToken ct = default);
}
