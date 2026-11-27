using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Billing;
using Alhecasa.Application.Common;
using Alhecasa.Application.Locations;
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

    /// <summary>Destaca o quita el destacado de una propiedad publicada. Destacar exige un paquete de destacados vigente con lugar libre.</summary>
    Task<PropertyDto> SetFeaturedAsync(Guid id, SetFeaturedRequest request, CancellationToken cancellationToken = default);

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
    IEntitlementService entitlements,
    ILocationResolver locationResolver) : IPropertyService
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
        var now = timeProvider.GetUtcNow();
        return result.Map(p => PublicPropertyService.ToDto(p, now));
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
        var property = new Property(agency.Id, await ToDataAsync(request, cancellationToken));

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

        property.Update(await ToDataAsync(request, cancellationToken));

        // Un anuncio que deja de estar publicado pierde el destacado: así no ocupa un lugar del paquete sin verse.
        if (property.Status != PropertyStatus.Published)
        {
            property.Unfeature();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(property.Id, cancellationToken);
    }

    public async Task<PropertyDto> SetFeaturedAsync(Guid id, SetFeaturedRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManageProperties();

        var property = await properties.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Property not found.");
        var now = timeProvider.GetUtcNow();

        if (!request.Featured)
        {
            property.Unfeature();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ToDto(property);
        }

        if (property.Status != PropertyStatus.Published)
        {
            throw new DomainException("Only a published property can be featured.");
        }

        if (property.IsFeaturedAt(now))
        {
            return ToDto(property);
        }

        var package = await entitlements.GetFeaturedAsync(property.AgencyId, cancellationToken)
            ?? throw new PlanLimitException("Tu cuenta no tiene un paquete de destacados vigente. Contrata uno para destacar tus anuncios.");

        if (package.Limit is { } limit && package.Used >= limit)
        {
            throw new PlanLimitException(
                $"Tu paquete \"{package.PlanName}\" permite {limit} {(limit == 1 ? "anuncio destacado" : "anuncios destacados")} a la vez. Quita el destacado a otro o contrata un paquete mayor.");
        }

        property.Feature(package.EndsAt, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(property);
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

    /// <summary>
    /// Los datos de la propiedad con su ubicación enlazada al catálogo de estados, ciudades y colonias, con los nombres oficiales del catálogo
    /// (así «cdmx» y «Ciudad de México», o «polanco» y «Polanco», son la misma zona y forman una sola página pública).
    /// </summary>
    private async Task<PropertyData> ToDataAsync(PropertyRequest request, CancellationToken cancellationToken)
    {
        var location = await locationResolver.ResolveAsync(request.State, request.City, request.Neighborhood, cancellationToken);
        return request.ToData() with
        {
            State = location.StateName,
            City = location.CityName,
            Neighborhood = location.NeighborhoodName,
            StateId = location.StateId,
            CityId = location.CityId,
            NeighborhoodId = location.NeighborhoodId
        };
    }

    private const string PublishNeedsImagesMessage =
        "A published property needs at least one image. Save it as a draft, add the images, then publish it.";

    private static ImageFormat ValidateImage(ImageUpload upload) => PropertyImageRules.Validate(upload);

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

    /// <summary>El plan de la cuenta (gratuito, de pago o ninguno) fija cuántas propiedades puede tener, borradores incluidos.</summary>
    private async Task EnsureWithinPlanAsync(Agency agency, CancellationToken cancellationToken)
    {
        var allowance = await entitlements.GetListingsAsync(agency, cancellationToken);
        if (allowance.Limit is not { } limit || allowance.Used < limit)
        {
            return;
        }

        var noun = limit == 1 ? "propiedad" : "propiedades";
        throw new PlanLimitException(allowance.IsFree
            ? $"Tu cuenta gratuita permite hasta {limit} {noun}. Elimina una para publicar otra."
            : $"Tu plan \"{allowance.PlanName}\" permite hasta {limit} {noun}. Elimina una o contrata un plan mayor.");
    }

    private PropertyDto ToDto(Property p) => new(
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
        AmenityFlags.ToList(p.Amenities),
        p.IsFeaturedAt(timeProvider.GetUtcNow()),
        p.IsFeaturedAt(timeProvider.GetUtcNow()) ? p.FeaturedUntil : null);
}
