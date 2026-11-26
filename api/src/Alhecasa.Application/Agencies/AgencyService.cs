using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using FluentValidation;

namespace Alhecasa.Application.Agencies;

public sealed record AgencyDto(Guid Id, string Name, string Email, string? Phone, bool IsActive, bool IsIndividual = false);

/// <summary>El perfil de la propia inmobiliaria, tal como lo edita su administrador.</summary>
public sealed record AgencyProfileDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string? Description,
    string? Website,
    string? FacebookUrl,
    string? InstagramUrl,
    string? OpeningHours,
    string? Street,
    string? City,
    string? State,
    string? PostalCode,
    bool HasLogo);

/// <summary>Datos del perfil que el administrador de la inmobiliaria puede cambiar. El logotipo se sube aparte.</summary>
public sealed record UpdateAgencyProfileRequest(
    string Name,
    string Email,
    string? Phone,
    string? Description,
    string? Website,
    string? FacebookUrl,
    string? InstagramUrl,
    string? OpeningHours,
    string? Street,
    string? City,
    string? State,
    string? PostalCode);

public interface IAgencyService
{
    Task<IReadOnlyList<AgencyDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Perfil de la inmobiliaria del administrador autenticado.</summary>
    Task<AgencyProfileDto> GetMineAsync(CancellationToken cancellationToken = default);

    /// <summary>Reemplaza los datos del perfil de la inmobiliaria del administrador autenticado.</summary>
    Task<AgencyProfileDto> UpdateMineAsync(UpdateAgencyProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sube o reemplaza el logotipo (JPEG, PNG o WebP de hasta 5 MB).</summary>
    Task<AgencyProfileDto> SetLogoAsync(ImageUpload upload, CancellationToken cancellationToken = default);

    /// <summary>Quita el logotipo. Si no tenía, no hace nada.</summary>
    Task<AgencyProfileDto> RemoveLogoAsync(CancellationToken cancellationToken = default);
}

public sealed class AgencyService(
    IAgencyRepository agencies,
    IUnitOfWork unitOfWork,
    IImageStorage imageStorage,
    ICurrentUser currentUser,
    IValidator<UpdateAgencyProfileRequest> profileValidator) : IAgencyService
{
    // El filtro global de aislamiento hace que un AgencyAdmin solo vea su inmobiliaria.
    public async Task<IReadOnlyList<AgencyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await agencies.ListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<AgencyProfileDto> GetMineAsync(CancellationToken cancellationToken = default) =>
        ToProfile(await GetMineEntityAsync(cancellationToken));

    public async Task<AgencyProfileDto> UpdateMineAsync(UpdateAgencyProfileRequest request, CancellationToken cancellationToken = default)
    {
        var agency = await GetMineEntityAsync(cancellationToken);
        await profileValidator.ValidateAndThrowAsync(request, cancellationToken);

        agency.UpdateProfile(new AgencyProfile(
            request.Name,
            request.Email,
            request.Phone,
            request.Description,
            request.Website,
            request.FacebookUrl,
            request.InstagramUrl,
            request.OpeningHours,
            request.Street,
            request.City,
            request.State,
            request.PostalCode));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToProfile(agency);
    }

    public async Task<AgencyProfileDto> SetLogoAsync(ImageUpload upload, CancellationToken cancellationToken = default)
    {
        var agency = await GetMineEntityAsync(cancellationToken);

        // Se valida antes de tocar el almacenamiento, para no dejar archivos a medias.
        var format = PropertyImageRules.Validate(upload);

        var key = $"{Guid.CreateVersion7():N}.{format.Extension}";
        upload.Content.Position = 0;
        await imageStorage.SaveAsync(key, upload.Content, cancellationToken);

        string? previous;
        try
        {
            previous = agency.SetLogo(key, format.ContentType);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Si no se pudo guardar en la base, el archivo nuevo no tiene dueño.
            await imageStorage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        // La base ya quedó guardada: un archivo viejo que no se pueda borrar queda huérfano, pero no invalida la operación.
        if (previous is not null)
        {
            await imageStorage.DeleteAsync(previous, cancellationToken);
        }

        return ToProfile(agency);
    }

    public async Task<AgencyProfileDto> RemoveLogoAsync(CancellationToken cancellationToken = default)
    {
        var agency = await GetMineEntityAsync(cancellationToken);

        var previous = agency.RemoveLogo();
        if (previous is not null)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await imageStorage.DeleteAsync(previous, cancellationToken);
        }

        return ToProfile(agency);
    }

    /// <summary>
    /// Solo el administrador de una inmobiliaria edita su perfil. Un particular no tiene página pública (es anónimo)
    /// y el administrador de plataforma no pertenece a ninguna inmobiliaria.
    /// </summary>
    private async Task<Agency> GetMineEntityAsync(CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.AgencyAdmin || currentUser.AgencyId is not { } agencyId)
        {
            throw new ForbiddenException("Only the administrator of an agency can edit its information.");
        }

        var agency = await agencies.GetByIdAsync(agencyId, cancellationToken)
            ?? throw new NotFoundException("Agency not found.");

        if (agency.IsIndividual)
        {
            throw new ForbiddenException("A free individual account has no agency profile.");
        }

        return agency;
    }

    private static AgencyDto ToDto(Agency a) => new(a.Id, a.Name, a.Email, a.Phone, a.IsActive, a.IsIndividual);

    internal static AgencyProfileDto ToProfile(Agency a) => new(
        a.Id,
        a.Name,
        a.Email,
        a.Phone,
        a.Description,
        a.Website,
        a.FacebookUrl,
        a.InstagramUrl,
        a.OpeningHours,
        a.Street,
        a.City,
        a.State,
        a.PostalCode,
        a.HasLogo);
}

public sealed class UpdateAgencyProfileRequestValidator : AbstractValidator<UpdateAgencyProfileRequest>
{
    public UpdateAgencyProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(AgencyProfileLimits.Name);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(AgencyProfileLimits.Email).EmailAddress();

        // Solo dígitos y los signos de un teléfono; el largo lo da el esquema.
        RuleFor(x => x.Phone)
            .MaximumLength(AgencyProfileLimits.Phone)
            .Matches(@"^[0-9+()\-.\s]{7,}$").WithMessage("'Phone' must be a valid phone number.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Description).MaximumLength(AgencyProfileLimits.Description);
        RuleFor(x => x.Website).WebAddress();
        RuleFor(x => x.FacebookUrl).WebAddress();
        RuleFor(x => x.InstagramUrl).WebAddress();
        RuleFor(x => x.OpeningHours).MaximumLength(AgencyProfileLimits.OpeningHours);
        RuleFor(x => x.Street).MaximumLength(AgencyProfileLimits.Street);
        RuleFor(x => x.City).MaximumLength(AgencyProfileLimits.City);
        RuleFor(x => x.State).MaximumLength(AgencyProfileLimits.State);
        RuleFor(x => x.PostalCode).MaximumLength(AgencyProfileLimits.PostalCode);
    }
}

internal static class AgencyRules
{
    /// <summary>
    /// Opcional; si viene, debe ser una dirección http o https. Se rechaza cualquier otro esquema (<c>javascript:</c>, <c>data:</c>…)
    /// porque estos enlaces se publican en la página de la inmobiliaria.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> WebAddress<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(AgencyProfileLimits.Url)
            .Must(value => string.IsNullOrWhiteSpace(value) || IsWebAddress(value))
            .WithMessage("'{PropertyName}' must be a web address that starts with http:// or https://.");

    internal static bool IsWebAddress(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);
}