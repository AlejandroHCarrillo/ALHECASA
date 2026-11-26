using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Application.Abstractions;

/// <summary>Catálogo de planes de la plataforma. No está aislado por inmobiliaria; excluye los eliminados.</summary>
public interface IPlanRepository
{
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Todos los planes no eliminados (activos e inactivos), opcionalmente de un solo tipo. Los de pago antes que el gratuito, por tipo y nombre.</summary>
    Task<IReadOnlyList<Plan>> ListAsync(PlanKind? kind = null, CancellationToken cancellationToken = default);

    /// <summary>El plan gratuito activo (el que tienen por omisión los particulares), o null si no hay.</summary>
    Task<Plan?> GetActiveFreeAsync(CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(Plan plan, CancellationToken cancellationToken = default);
}

/// <summary>Contrataciones. Respetan el aislamiento por inmobiliaria: el personal solo ve las de su cuenta.</summary>
public interface ISubscriptionRepository
{
    /// <summary>La contratación vigente de un tipo para una cuenta (no cancelada, ya iniciada y sin vencer), o null.</summary>
    Task<Subscription?> GetActiveAsync(Guid agencyId, PlanKind kind, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Todas las contrataciones de una cuenta, la más reciente primero.</summary>
    Task<IReadOnlyList<Subscription>> ListByAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default);

    /// <summary>Las contrataciones vigentes de todas las cuentas (para el tablero del administrador de plataforma).</summary>
    Task<IReadOnlyList<Subscription>> ListActiveAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);
}

/// <summary>Cuántas propiedades tiene una cuenta y cuántas están destacadas.</summary>
public sealed record PropertyUsage(int Total, int Featured);
