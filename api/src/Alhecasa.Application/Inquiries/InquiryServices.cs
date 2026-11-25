using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Inquiries;

/// <summary>Alta de una solicitud de informes por parte de un visitante, sin sesión.</summary>
public interface IPublicInquiryService
{
    /// <summary>
    /// Registra la solicitud para la inmobiliaria dueña de la propiedad.
    /// <see cref="NotFoundException"/> si la propiedad no existe o no está publicada (no se distingue el motivo).
    /// </summary>
    Task SubmitAsync(Guid propertyId, CreateInquiryRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Bandeja de solicitudes de informes de la inmobiliaria del usuario.</summary>
public interface IInquiryService
{
    Task<PagedResult<InquiryDto>> GetPagedAsync(InquiryListQuery query, CancellationToken cancellationToken = default);

    Task<InquiryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registra una respuesta al prospecto o una nota interna. Una respuesta pasa la solicitud a «En seguimiento».</summary>
    Task<InquiryDto> AddFollowUpAsync(Guid id, AddFollowUpRequest request, CancellationToken cancellationToken = default);

    Task<InquiryDto> ChangeStatusAsync(Guid id, ChangeInquiryStatusRequest request, CancellationToken cancellationToken = default);
}

public sealed class PublicInquiryService(
    IPropertyRepository properties,
    IInquiryRepository inquiries,
    IUnitOfWork unitOfWork,
    IValidator<CreateInquiryRequest> validator) : IPublicInquiryService
{
    public async Task SubmitAsync(Guid propertyId, CreateInquiryRequest request, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        // Las mismas reglas de visibilidad que el catálogo público: no se pide informes de lo que el visitante no puede ver.
        var property = await properties.GetPublishedByIdAsync(propertyId, cancellationToken)
            ?? throw new NotFoundException("Property not found.");

        var inquiry = new Inquiry(
            property.Id,
            property.AgencyId,
            property.Title,
            request.Name,
            request.Phone,
            EmailNormalizer.Normalize(request.Email),
            request.Message);

        await inquiries.AddAsync(inquiry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Reglas de autorización: solo el personal de una inmobiliaria (administrador, supervisor y vendedor) atiende las solicitudes,
/// y solo las de la suya (lo garantiza además el filtro global del repositorio). El administrador de plataforma no pertenece
/// a una inmobiliaria y los clientes no administran nada: ninguno de los dos entra.
/// </summary>
public sealed class InquiryService(
    IInquiryRepository inquiries,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<AddFollowUpRequest> followUpValidator,
    IValidator<ChangeInquiryStatusRequest> statusValidator) : IInquiryService
{
    public async Task<PagedResult<InquiryDto>> GetPagedAsync(InquiryListQuery query, CancellationToken cancellationToken = default)
    {
        EnsureIsAgencyStaff();
        var page = await inquiries.GetPagedAsync(query.Normalize(), cancellationToken);
        return page.Map(InquiryMapping.ToDto);
    }

    public async Task<InquiryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureIsAgencyStaff();
        return InquiryMapping.ToDto(await FindAsync(id, cancellationToken));
    }

    public async Task<InquiryDto> AddFollowUpAsync(Guid id, AddFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        var authorId = EnsureIsAgencyStaff();
        await followUpValidator.ValidateAndThrowAsync(request, cancellationToken);

        var inquiry = await FindAsync(id, cancellationToken);
        var author = await users.GetByIdAsync(authorId, cancellationToken)
            ?? throw new ForbiddenException("Your account is not available.");

        inquiry.AddFollowUp(request.Kind, request.Text, author.Id, author.FullName);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return InquiryMapping.ToDto(inquiry);
    }

    public async Task<InquiryDto> ChangeStatusAsync(Guid id, ChangeInquiryStatusRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIsAgencyStaff();
        await statusValidator.ValidateAndThrowAsync(request, cancellationToken);

        var inquiry = await FindAsync(id, cancellationToken);
        inquiry.ChangeStatus(request.Status);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return InquiryMapping.ToDto(inquiry);
    }

    private async Task<Inquiry> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await inquiries.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Inquiry not found.");

    /// <summary>Devuelve el identificador del usuario si es personal de una inmobiliaria; si no, <see cref="ForbiddenException"/>.</summary>
    private Guid EnsureIsAgencyStaff()
    {
        if (currentUser.Role is not (UserRole.AgencyAdmin or UserRole.Supervisor or UserRole.Seller)
            || currentUser.AgencyId is null
            || currentUser.UserId is not { } userId)
        {
            throw new ForbiddenException("Only agency staff can handle inquiries.");
        }

        return userId;
    }
}