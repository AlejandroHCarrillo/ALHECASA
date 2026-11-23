using Alhecasa.Application.Abstractions;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.Infrastructure.Persistence;

public sealed class AlhecasaDbContext(
    DbContextOptions<AlhecasaDbContext> options,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Agency> Agencies => Set<Agency>();

    // Propiedades del contexto usadas por los filtros globales: EF las evalúa en cada consulta
    // con los valores del usuario de la solicitud actual.
    private bool CurrentIsPlatformAdmin => currentUser.IsPlatformAdmin;

    private Guid? CurrentAgencyId => currentUser.AgencyId;

    private Guid? CurrentUserId => currentUser.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlhecasaDbContext).Assembly);

        // Borrado lógico + aislamiento por inmobiliaria (multi-tenant).
        // Sin usuario autenticado, los filtros no devuelven nada; el login usa IgnoreQueryFilters().
        modelBuilder.Entity<Agency>().HasQueryFilter(a =>
            !a.IsDeleted
            && (CurrentIsPlatformAdmin || (CurrentAgencyId != null && a.Id == CurrentAgencyId)));

        modelBuilder.Entity<User>().HasQueryFilter(u =>
            !u.IsDeleted
            && (CurrentIsPlatformAdmin
                || u.Id == CurrentUserId
                || (CurrentAgencyId != null && u.AgencyId == CurrentAgencyId)));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditing();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditing();
        return base.SaveChanges();
    }

    private void ApplyAuditing()
    {
        var now = timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
