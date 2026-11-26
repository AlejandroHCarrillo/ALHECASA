using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

public interface IAgencyRepository
{
    Task<Agency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Agency>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Una inmobiliaria visible para el público: activa, no eliminada y que no sea un particular. Ignora el aislamiento por inmobiliaria.</summary>
    Task<Agency?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Agency agency, CancellationToken cancellationToken = default);
}
