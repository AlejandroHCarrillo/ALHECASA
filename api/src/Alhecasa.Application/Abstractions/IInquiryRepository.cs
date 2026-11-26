using Alhecasa.Application.Common;
using Alhecasa.Application.Dashboard;
using Alhecasa.Application.Inquiries;
using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

public interface IInquiryRepository
{
    /// <summary>Una solicitud con su seguimiento, respetando el aislamiento por inmobiliaria. La entidad queda rastreada para poder modificarla.</summary>
    Task<Inquiry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Solicitudes de la inmobiliaria del usuario con su seguimiento, las más recientes primero.</summary>
    Task<PagedResult<Inquiry>> GetPagedAsync(InquiryListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Solicitudes de una inmobiliaria por etapa y cuántas llegaron desde <paramref name="since"/>.</summary>
    Task<InquiryStats> GetStatsAsync(Guid agencyId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>Las solicitudes nuevas (nadie las ha atendido) más recientes de una inmobiliaria, la más reciente primero.</summary>
    Task<IReadOnlyList<Inquiry>> ListLatestNewAsync(Guid agencyId, int count, CancellationToken cancellationToken = default);

    Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
}