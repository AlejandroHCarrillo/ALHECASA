using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence.Repositories;

internal sealed class PlanRepository(AlhecasaDbContext db) : IPlanRepository
{
    public Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Plans.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Plan>> ListAsync(PlanKind? kind = null, CancellationToken cancellationToken = default)
    {
        var query = db.Plans.AsNoTracking().Where(p => !p.IsDeleted);
        if (kind is not null)
        {
            query = query.Where(p => p.Kind == kind);
        }

        // El gratuito (precio 0) primero, luego por tipo, tope y nombre. Un tope nulo (ilimitado) va al final de su tipo.
        return await query
            .OrderBy(p => p.Price != 0)
            .ThenBy(p => p.Kind)
            .ThenBy(p => p.Limit == null)
            .ThenBy(p => p.Limit)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Plan?> GetActiveFreeAsync(CancellationToken cancellationToken = default) =>
        db.Plans.FirstOrDefaultAsync(p => !p.IsDeleted && p.IsActive && p.Price == 0, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default) =>
        db.Plans.AnyAsync(p => !p.IsDeleted && p.Name == name && p.Id != excludeId, cancellationToken);

    public async Task AddAsync(Plan plan, CancellationToken cancellationToken = default) =>
        await db.Plans.AddAsync(plan, cancellationToken);
}

internal sealed class SubscriptionRepository(AlhecasaDbContext db) : ISubscriptionRepository
{
    public Task<Subscription?> GetActiveAsync(Guid agencyId, PlanKind kind, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        db.Subscriptions
            .Where(s => s.AgencyId == agencyId && s.Kind == kind && s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)
            .OrderByDescending(s => s.StartsAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListByAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default) =>
        await db.Subscriptions.AsNoTracking()
            .Where(s => s.AgencyId == agencyId)
            .OrderByDescending(s => s.StartsAt)
            .ThenByDescending(s => s.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await db.Subscriptions.AsNoTracking()
            .Where(s => s.CancelledAt == null && s.StartsAt <= now && s.EndsAt > now)
            .OrderByDescending(s => s.StartsAt)
            .ToListAsync(cancellationToken);

    public Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default) =>
        await db.Subscriptions.AddAsync(subscription, cancellationToken);
}
