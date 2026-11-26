using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;

namespace Alhecasa.Application.Agencies;

/// <summary>
/// Lo que un visitante ve de una inmobiliaria: lo que su administrador configuró para mostrarse en su página pública.
/// Un particular nunca tiene perfil público (es anónimo, se publica como «Particular»).
/// </summary>
public sealed record PublicAgencyDto(
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

/// <summary>Logotipo listo para enviarse; <paramref name="ETag"/> cambia cuando cambia la imagen.</summary>
public sealed record AgencyLogo(Stream Content, string ContentType, string ETag);

public interface IPublicAgencyService
{
    /// <summary><see cref="NotFoundException"/> si no existe, está inactiva, eliminada o es un particular.</summary>
    Task<PublicAgencyDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Abre el logotipo, o devuelve null si la inmobiliaria no es visible o no tiene.</summary>
    Task<AgencyLogo?> OpenLogoAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// No depende del usuario actual (no hay sesión): el repositorio ignora el aislamiento por inmobiliaria y aplica
/// por su cuenta las reglas de visibilidad (inmobiliaria activa, no eliminada y que no sea un particular).
/// </summary>
public sealed class PublicAgencyService(IAgencyRepository agencies, IImageStorage storage) : IPublicAgencyService
{
    public async Task<PublicAgencyDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await agencies.GetPublicByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Agency not found.");

        return new PublicAgencyDto(a.Id, a.Name, a.Email, a.Phone, a.Description, a.Website, a.FacebookUrl, a.InstagramUrl,
            a.OpeningHours, a.Street, a.City, a.State, a.PostalCode, a.HasLogo);
    }

    public async Task<AgencyLogo?> OpenLogoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var agency = await agencies.GetPublicByIdAsync(id, cancellationToken);
        if (agency is not { LogoStorageKey: { } key, LogoContentType: { } contentType })
        {
            return null;
        }

        var content = await storage.OpenReadAsync(key, cancellationToken);

        // La clave es única por archivo (se genera al subirlo), así que sirve como versión del logotipo.
        return content is null ? null : new AgencyLogo(content, contentType, key);
    }
}