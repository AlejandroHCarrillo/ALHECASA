using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Properties;

public interface IPropertyService
{
    Task<PagedResult<PropertyDto>> GetPagedAsync(PropertyListQuery query, CancellationToken cancellationToken = default);

    Task<PropertyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Las propiedades publicadas de la inmobiliaria del usuario, con el mismo contenido y el mismo orden con que las ve un visitante
    /// (solo datos públicos). Solo para personal de una inmobiliaria: el administrador de plataforma no tiene una.
    /// </summary>
    Task<PagedResult<PublicPropertyDto>> GetGalleryAsync(PublicPropertyQuery query, CancellationToken cancellationToken = default);

    Task<PropertyDto> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default);

    Task<PropertyDto> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la galería de una propiedad en una sola operación: quita las imágenes indicadas y agrega las nuevas.
    /// El resultado final debe tener como máximo 10 imágenes y, si la propiedad está publicada, al menos una.
    /// </summary>
    Task<PropertyDto> UpdateImagesAsync(
        Guid id,
        IReadOnlyCollection<ImageUpload> uploads,
        IReadOnlyCollection<Guid> removeImageIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inventario de propiedades de la inmobiliaria.
/// Reglas de autorización:
/// - PlatformAdmin, AgencyAdmin, Supervisor y Seller administran propiedades; Client no.
/// - Un usuario de inmobiliaria solo ve y modifica las de la suya (lo garantiza además el filtro global del repositorio).
/// - Solo PlatformAdmin elige la inmobiliaria al crear.
/// Imágenes: de 0 a 10 por propiedad; una propiedad publicada necesita al menos una. Por eso una propiedad
/// nueva no puede nacer publicada (aún no tiene imágenes): se crea como borrador, se suben las imágenes y se publica.
/// </summary>
public sealed class PropertyService(
    IPropertyRepository properties,
    IAgencyRepository agencies,
    IImageStorage imageStorage,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<CreatePropertyRequest> createValidator,
    IValidator<UpdatePropertyRequest> updateValidator,
    TimeProvider timeProvider,
    AccountLimits accountLimits) : IPropertyService
{
    public async Task<PagedResult<PropertyDto>> GetPagedAsync(PropertyListQuery query, CancellationToken cancellationToken = default)
    {
        EnsureCanManageProperties();
        var page = await properties.GetPagedAsync(query.Normalize(), cancellationToken);
        return page.Map(ToDto);
    }

    public async Task<PagedResult<PublicPropertyDto>> GetGalleryAsync(PublicPropertyQuery query, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role is not (UserRole.AgencyAdmin or UserRole.Supervisor or UserRole.Seller))
        {
            throw new ForbiddenException("Only agency staff have a gallery.");
        }

        var agencyId = currentUser.AgencyId
            ?? throw new ForbiddenException("Your account does not belong to an agency.");

        var result = await properties.GetPublishedPagedAsync(query.Normalize() with { Ids = null }, agencyId, cancellationToken);
        return result.Map(PublicPropertyService.ToDto);
    }

    public async Task<PropertyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanManageProperties();
        var property = await properties.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Property not found.");
        return ToDto(property);
    }

    public async Task<PropertyDto> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManageProperties();
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.Status == PropertyStatus.Published)
        {
            throw new DomainException(PublishNeedsImagesMessage);
        }

        var agency = await ResolveAgencyAsync(request.AgencyId, cancellationToken);
        await EnsureWithinPlanAsync(agency, cancellationToken);
        var property = new Property(agency.Id, request.ToData());

        await properties.AddAsync(property, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(property.Id, cancellationToken);
    }

    public async Task<PropertyDto> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManageProperties();
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var property = await properties.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Property not found.");

        if (request.Status == PropertyStatus.Published && property.Images.Count == 0)
        {
            throw new DomainException(PublishNeedsImagesMessage);
        }

        property.Update(request.ToData());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(property.Id, cancellationToken);
    }

    public async Task<PropertyDto> UpdateImagesAsync(
        Guid id,
        IReadOnlyCollection<ImageUpload> uploads,
        IReadOnlyCollection<Guid> removeImageIds,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManageProperties();

        var property = await properties.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Property not found.");

        var toRemove = removeImageIds.Distinct().ToList();
        if (toRemove.Any(imageId => property.Images.All(i => i.Id != imageId)))
        {
            throw new DomainException("One of the images to remove does not belong to this property.");
        }

        var finalCount = property.Images.Count - toRemove.Count + uploads.Count;
        if (finalCount > Property.MaxImages)
        {
            throw new DomainException($"A property can have at most {Property.MaxImages} images.");
        }

        if (finalCount == 0 && property.Status == PropertyStatus.Published)
        {
            throw new DomainException("A published property needs at least one image. Pause it or add an image first.");
        }

        // Se valida todo antes de tocar el almacenamiento, para no dejar archivos a medias.
        var accepted = new List<(ImageUpload Upload, ImageFormat Format)>();
        foreach (var upload in uploads)
        {
            accepted.Add((upload, ValidateImage(upload)));
        }

        var savedKeys = new List<string>();
        try
        {
            // Primero se quitan las que sobran: así una galería llena puede cambiar una foto en un solo paso.
            var removedKeys = toRemove.Select(imageId => property.RemoveImage(imageId)!.StorageKey).ToList();

            foreach (var (upload, format) in accepted)
            {
                var key = $"{Guid.CreateVersion7():N}.{format.Extension}";
                upload.Content.Position = 0;
                await imageStorage.SaveAsync(key, upload.Content, cancellationToken);
                savedKeys.Add(key);
                property.AddImage(key, format.ContentType, upload.Length);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            // La base ya quedó guardada: un archivo que no se pueda borrar queda huérfano, pero no invalida la operación.
            foreach (var key in removedKeys)
            {
                await imageStorage.DeleteAsync(key, cancellationToken);
            }
        }
        catch
        {
            // Si algo falló antes de confirmar, no se dejan archivos nuevos sin dueño.
            foreach (var key in savedKeys)
            {
                await imageStorage.DeleteAsync(key, CancellationToken.None);
            }

            throw;
        }

        return ToDto(property);
    }

    private const string PublishNeedsImagesMessage =
        "A published property needs at least one image. Save it as a draft, add the images, then publish it.";

    private static ImageFormat ValidateImage(ImageUpload upload)
    {
        if (upload.Length <= 0)
        {
            throw new DomainException("An image file is empty.");
        }

        if (upload.Length > PropertyImageRules.MaxBytes)
        {
            throw new DomainException($"Each image can be at most {PropertyImageRules.MaxBytes / (1024 * 1024)} MB.");
        }

        if (!upload.Content.CanSeek)
        {
            throw new DomainException("The image could not be read.");
        }

        Span<byte> header = stackalloc byte[PropertyImageRules.HeaderLength];
        upload.Content.Position = 0;
        var read = upload.Content.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        return PropertyImageRules.Detect(header[..read])
            ?? throw new DomainException("Only JPEG, PNG and WebP images are accepted.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureCanManageProperties();

        var property = await properties.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Property not found.");

        property.MarkAsDeleted(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void EnsureCanManageProperties()
    {
        if (currentUser.Role is not (UserRole.PlatformAdmin or UserRole.AgencyAdmin or UserRole.Supervisor or UserRole.Seller))
        {
            throw new ForbiddenException("You do not have permission to manage properties.");
        }
    }

    private async Task<Agency> ResolveAgencyAsync(Guid? requestedAgencyId, CancellationToken cancellationToken)
    {
        Guid agencyId;

        if (currentUser.IsPlatformAdmin)
        {
            agencyId = requestedAgencyId
                ?? throw new DomainException("An agency is required to create a property.");
        }
        else
        {
            if (requestedAgencyId is not null && requestedAgencyId != currentUser.AgencyId)
            {
                throw new ForbiddenException("You cannot create properties for another agency.");
            }

            agencyId = currentUser.AgencyId
                ?? throw new ForbiddenException("Your account does not belong to an agency.");
        }

        return await agencies.GetByIdAsync(agencyId, cancellationToken)
            ?? throw new NotFoundException("Agency not found.");
    }

    /// <summary>La cuenta gratuita de un particular tiene un tope de propiedades (borradores incluidos).</summary>
    private async Task EnsureWithinPlanAsync(Agency agency, CancellationToken cancellationToken)
    {
        if (!agency.IsIndividual)
        {
            return;
        }

        var limit = accountLimits.IndividualMaxProperties;
        if (await properties.CountByAgencyAsync(agency.Id, cancellationToken) >= limit)
        {
            throw new PlanLimitException(
                $"Tu cuenta gratuita permite hasta {limit} {(limit == 1 ? "propiedad" : "propiedades")}. Elimina una para publicar otra.");
        }
    }

    private static PropertyDto ToDto(Property p) => new(
        p.Id,
        p.AgencyId,
        p.Agency?.Name,
        p.Title,
        p.Description,
        p.Type,
        p.Operation,
        p.Status,
        p.Price,
        p.Currency,
        p.Street,
        p.Neighborhood,
        p.City,
        p.State,
        p.PostalCode,
        p.Bedrooms,
        p.Bathrooms,
        p.ConstructionArea,
        p.LandArea,
        p.Latitude,
        p.Longitude,
        PublicPropertyService.OrderedImageIds(p),
        p.CreatedAt,
        p.UpdatedAt,
        p.ParkingSpaces,
        p.PetsAllowed,
        p.Furnished,
        AmenityFlags.ToList(p.Amenities));
}
