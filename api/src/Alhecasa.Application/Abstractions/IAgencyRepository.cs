using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

public interface IAgencyRepository
{
    Task<Agency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Agency>> ListAsync(CancellationToken cancellationToken = default);
}
