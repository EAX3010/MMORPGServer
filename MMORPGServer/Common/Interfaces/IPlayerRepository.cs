using MMORPGServer.Entities;

namespace MMORPGServer.Common.Interfaces
{
    public interface IPlayerRepository
    {
        Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default(CancellationToken));
        Task<Player?> GetByIdAsync(int Id, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> UpsertPlayerAsync(Player player, CancellationToken cancellationToken = default(CancellationToken));
    }
}