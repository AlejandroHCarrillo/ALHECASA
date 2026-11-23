using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;

namespace Alhecasa.Application.Agencies;

public sealed record AgencyDto(Guid Id, string Name, string Email, string? Phone, bool IsActive);

public interface IAgencyService
{
    Task<IReadOnlyList<AgencyDto>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed class AgencyService(IAgencyRepository agencies) : IAgencyService
{
    // El filtro global de aislamiento hace que un AgencyAdmin solo vea su inmobiliaria.
    public async Task<IReadOnlyList<AgencyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await agencies.ListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    private static AgencyDto ToDto(Agency a) => new(a.Id, a.Name, a.Email, a.Phone, a.IsActive);
}
