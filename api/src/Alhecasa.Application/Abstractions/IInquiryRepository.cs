using Alhecasa.Application.Common;
using Alhecasa.Application.Inquiries;
using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Abstractions;

public interface IInquiryRepository
{
    /// <summary>Una solicitud con su seguimiento, respetando el aislamiento por inmobiliaria. La entidad queda rastreada para poder modificarla.</summary>
    Task<Inquiry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Solicitudes de la inmobiliaria del usuario con su seguimiento, las más recientes primero.</summary>
    Task<PagedResult<Inquiry>> GetPagedAsync(InquiryListQuery query, CancellationToken cancellationToken = default);

    Task AddAsync(Inquiry inquiry, CancellationToken cancellationToken = default);
}