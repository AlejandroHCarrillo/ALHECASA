using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence.Repositories;

internal sealed class AgencyRepository(AlhecasaDbContext db) : IAgencyRepository
{
    public Task<Agency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Agencies.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Agency?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Agencies.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted && a.IsActive && a.Kind != AgencyKind.Individual, cancellationToken);

    public async Task AddAsync(Agency agency, CancellationToken cancellationToken = default) =>
        await db.Agencies.AddAsync(agency, cancellationToken);

    public async Task<IReadOnlyList<Agency>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Agencies.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);
}
