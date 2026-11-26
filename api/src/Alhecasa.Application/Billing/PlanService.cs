using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Billing;

public interface IPlanService
{
    Task<IReadOnlyList<PlanDto>> ListAsync(PlanKind? kind = null, CancellationToken cancellationToken = default);

    Task<PlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PlanDto> CreateAsync(SavePlanRequest request, CancellationToken cancellationToken = default);

    Task<PlanDto> UpdateAsync(Guid id, SavePlanRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Catálogo de planes para monetizar el portal. Solo lo administra el administrador de plataforma.
/// Reglas: el nombre es único; solo puede haber un plan gratuito activo (el de los particulares) y no se puede eliminar
/// ni volver de pago; un plan eliminado deja de ofrecerse pero las contrataciones que ya existen conservan lo que pagaron.
/// </summary>
public sealed class PlanService(
    IPlanRepository plans,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<SavePlanRequest> validator,
    TimeProvider timeProvider) : IPlanService
{
    public async Task<IReadOnlyList<PlanDto>> ListAsync(PlanKind? kind = null, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();
        if (kind is { } k && !Enum.IsDefined(k))
        {
            throw new DomainException("The plan kind is not valid.");
        }

        return (await plans.ListAsync(kind, cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<PlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();
        return ToDto(await FindAsync(id, cancellationToken));
    }

    public async Task<PlanDto> CreateAsync(SavePlanRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var plan = new Plan(request.ToData());
        await EnsureNameIsFreeAsync(plan.Name, null, cancellationToken);
        await EnsureSingleFreePlanAsync(plan, cancellationToken);

        await plans.AddAsync(plan, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    public async Task<PlanDto> UpdateAsync(Guid id, SavePlanRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var plan = await FindAsync(id, cancellationToken);
        if (plan.IsFree && request.Price != 0)
        {
            throw new DomainException("The free plan cannot become a paid plan. Edit its limit, or create another plan.");
        }

        plan.Update(request.ToData());
        await EnsureNameIsFreeAsync(plan.Name, plan.Id, cancellationToken);
        await EnsureSingleFreePlanAsync(plan, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsurePlatformAdmin();

        var plan = await FindAsync(id, cancellationToken);
        if (plan.IsFree)
        {
            throw new DomainException("The free plan cannot be deleted. Edit its limit instead.");
        }

        plan.MarkAsDeleted(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Plan> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await plans.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Plan not found.");

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        if (await plans.NameExistsAsync(name, excludeId, cancellationToken))
        {
            throw new ConflictException("A plan with that name already exists.");
        }
    }

    private async Task EnsureSingleFreePlanAsync(Plan plan, CancellationToken cancellationToken)
    {
        if (plan.IsFree && await plans.GetActiveFreeAsync(cancellationToken) is { } existing && existing.Id != plan.Id)
        {
            throw new ConflictException($"There is already a free plan (\"{existing.Name}\"). Edit it instead.");
        }
    }

    private void EnsurePlatformAdmin()
    {
        if (!currentUser.IsPlatformAdmin)
        {
            throw new ForbiddenException("Only the platform administrator manages plans.");
        }
    }

    internal static PlanDto ToDto(Plan p) => new(
        p.Id, p.Name, p.Description, p.Kind, p.Audience, p.Limit, p.Period, p.Price, p.Currency, p.IsActive, p.IsFree, p.CreatedAt, p.UpdatedAt);
}
