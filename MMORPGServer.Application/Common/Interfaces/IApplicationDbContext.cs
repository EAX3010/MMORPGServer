using Microsoft.EntityFrameworkCore;
using MMORPGServer.Domain.Persistence;

namespace MMORPGServer.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<PlayerEntity> Players { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
