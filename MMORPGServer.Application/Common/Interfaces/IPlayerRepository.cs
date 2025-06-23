using MMORPGServer.Domain.Entities;

namespace MMORPGServer.Application.Common.Interfaces
{
    public interface IPlayerRepository
    {
        Task<Player?> GetByIdAsync(int Id, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> UpsertPlayerAsync(Player player, CancellationToken cancellationToken = default(CancellationToken));
    }
}