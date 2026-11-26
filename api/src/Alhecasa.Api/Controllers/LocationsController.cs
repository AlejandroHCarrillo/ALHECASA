using Alhecasa.Application.Locations;
using Microsoft.AspNetCore.Mvc;

namespace Alhecasa.Api.Controllers;

/// <summary>
/// Sugerencias de estados, ciudades y colonias para los formularios (el catálogo de ubicaciones). Requiere sesión.
/// El catálogo crece solo: al guardar una propiedad, lo que no existe se da de alta.
/// </summary>
[ApiController]
[Route("api/locations")]
public sealed class LocationsController(ILocationService locations) : ControllerBase
{
    /// <summary>Los estados de México y los que se hayan dado de alta, por orden alfabético.</summary>
    [HttpGet("states")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetStates(CancellationToken cancellationToken) =>
        Ok(await locations.ListStatesAsync(cancellationToken));

    /// <summary>Ciudades que contienen el texto, del estado indicado (por nombre o abreviatura) o de todos si no se indica. Hasta 50.</summary>
    [HttpGet("cities")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCities([FromQuery] string? state, [FromQuery] string? q, CancellationToken cancellationToken) =>
        Ok(await locations.SearchCitiesAsync(state, q, cancellationToken));

    /// <summary>Colonias que contienen el texto, de la ciudad indicada (estado y ciudad son obligatorios; sin ellos, la lista va vacía). Hasta 50.</summary>
    [HttpGet("neighborhoods")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetNeighborhoods(
        [FromQuery] string? state,
        [FromQuery] string? city,
        [FromQuery] string? q,
        CancellationToken cancellationToken) =>
        Ok(await locations.SearchNeighborhoodsAsync(state, city, q, cancellationToken));
}
