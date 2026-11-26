using System.Net;
using System.Xml.Linq;
using Alhecasa.Application.Common;
using Alhecasa.Application.Locations;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.Infrastructure.Persistence.Seeding;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Alhecasa.IntegrationTests;

/// <summary>Catálogo de ubicaciones, páginas públicas por zona, filtros por zona y mapa del sitio.</summary>
public sealed class LocationCatalogEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Las pruebas comparten la base: cada una usa ciudades propias para no mezclar sus conteos.
    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    private static string SlugOf(string name) => Alhecasa.Domain.Common.Slug.From(name);

    private async Task<Agency> AddCompanyAsync() => await factory.AddAgencyAsync(Unique("Inmobiliaria"));

    private static CreatePropertyRequest NewRequest(string state, string city, string? neighborhood) => new()
    {
        Title = "Casa de prueba",
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Price = 1_000_000m,
        State = state,
        City = city,
        Neighborhood = neighborhood
    };

    // --- Al guardar una propiedad -----------------------------------------------------------

    [Fact]
    public async Task SavingProperties_WithDifferentSpellings_LinksThemToTheSameLocation()
    {
        var agency = await AddCompanyAsync();
        var client = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id));
        var city = Unique("Villa");

        var first = await client.PostJsonAsync("/api/properties", NewRequest("CDMX", city, "Polanco"));
        var second = await client.PostJsonAsync("/api/properties", NewRequest("ciudad de mexico", city.ToUpperInvariant(), "  polanco "));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var a = await first.ReadAsync<PropertyDto>();
        var b = await second.ReadAsync<PropertyDto>();
        Assert.Equal("Ciudad de México", a.State);
        Assert.Equal(a.State, b.State);
        Assert.Equal(a.City, b.City);
        Assert.Equal("Polanco", b.Neighborhood);

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().Where(p => p.Id == a.Id || p.Id == b.Id).ToListAsync();
            Assert.All(stored, p => Assert.NotNull(p.CityId));
            Assert.Single(stored.Select(p => p.CityId).Distinct());
            Assert.Single(stored.Select(p => p.NeighborhoodId).Distinct());
            Assert.Single(await db.GeoCities.Where(c => c.Slug == SlugOf(city)).ToListAsync());
        });
    }

    [Fact]
    public async Task EditingAProperty_MovesItToTheNewLocation()
    {
        var agency = await AddCompanyAsync();
        var client = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id));
        var oldCity = Unique("Antigua");
        var newCity = Unique("Nueva");
        var created = await (await client.PostJsonAsync("/api/properties", NewRequest("Jalisco", oldCity, "Centro"))).ReadAsync<PropertyDto>();

        var updated = await client.PutJsonAsync($"/api/properties/{created.Id}", new UpdatePropertyRequest
        {
            Title = "Casa de prueba",
            Type = PropertyType.House,
            Operation = OperationType.Sale,
            Price = 1_000_000m,
            State = "Yucatán",
            City = newCity,
            Neighborhood = null
        });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var dto = await updated.ReadAsync<PropertyDto>();
        Assert.Equal("Yucatán", dto.State);
        Assert.Null(dto.Neighborhood);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == created.Id);
            var city = await db.GeoCities.SingleAsync(c => c.Id == stored.CityId);
            Assert.Equal(SlugOf(newCity), city.Slug);
            Assert.Null(stored.NeighborhoodId);
        });
    }

    [Fact]
    public async Task ACityNameWithoutLettersOrNumbers_IsRejected()
    {
        var agency = await AddCompanyAsync();
        var client = factory.CreateClientFor(await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id));

        var response = await client.PostJsonAsync("/api/properties", NewRequest("Jalisco", "???", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Sugerencias para los formularios ---------------------------------------------------

    [Fact]
    public async Task Suggestions_RequireASession()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/locations/states")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/locations/cities")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/locations/neighborhoods")).StatusCode);
    }

    [Fact]
    public async Task Suggestions_ListStatesCitiesAndNeighborhoods()
    {
        var agency = await AddCompanyAsync();
        var city = Unique("Ciudad");
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Providencia");
        await factory.AddLocatedPropertyAsync(agency.Id, "JAL", city, "Americana");
        var client = factory.CreateClientFor(await factory.AddUserAsync(UserRole.Seller, agency.Id));

        var states = await (await client.GetAsync("/api/locations/states")).ReadAsync<List<string>>();
        var cities = await (await client.GetAsync($"/api/locations/cities?state=Jalisco&q={Uri.EscapeDataString(city[..9])}")).ReadAsync<List<string>>();
        var neighborhoods = await (await client.GetAsync($"/api/locations/neighborhoods?state=jal&city={Uri.EscapeDataString(city)}")).ReadAsync<List<string>>();
        var searched = await (await client.GetAsync($"/api/locations/neighborhoods?state=jal&city={Uri.EscapeDataString(city)}&q=prov")).ReadAsync<List<string>>();

        Assert.Equal(32, states.Count(s => Alhecasa.Domain.Common.MexicanStates.Names.Contains(s)));
        Assert.Contains("Jalisco", states);
        Assert.Contains(city, cities);
        Assert.Equal(["Americana", "Providencia"], neighborhoods);
        Assert.Equal(["Providencia"], searched);
        Assert.Empty(await (await client.GetAsync("/api/locations/neighborhoods?city=x")).ReadAsync<List<string>>());
    }

    // --- Zonas públicas ---------------------------------------------------------------------

    [Fact]
    public async Task Zone_CountsOnlyWhatTheVisitorCanSee()
    {
        var agency = await AddCompanyAsync();
        var city = Unique("Zona");
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Sale);
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Sale);
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Rent);
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Norte", OperationType.Sale);
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Sale, PropertyStatus.Draft);
        var deleted = await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Sale);
        await factory.WithDbAsync(async db =>
        {
            (await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == deleted.Id)).MarkAsDeleted(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        var otherAgency = await AddCompanyAsync();
        await factory.AddLocatedPropertyAsync(otherAgency.Id, "Jalisco", city, "Centro", OperationType.Sale);
        await factory.ModifyAgencyAsync(otherAgency.Id, a => a.MarkAsDeleted(DateTimeOffset.UtcNow));
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/public/zones/{SlugOf(city)}");
        var neighborhood = await (await anonymous.GetAsync($"/api/public/zones/{SlugOf(city)}/centro")).ReadAsync<ZoneDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var zone = await response.ReadAsync<ZoneDto>();
        Assert.Equal(city, zone.CityName);
        Assert.Equal("Jalisco", zone.StateName);
        Assert.Null(zone.NeighborhoodName);
        Assert.Equal((3, 1), (zone.SaleCount, zone.RentCount));
        Assert.Equal(["centro", "norte"], zone.Neighborhoods.Select(n => n.NeighborhoodSlug));
        Assert.Equal("Centro", neighborhood.NeighborhoodName);
        Assert.Equal((2, 1), (neighborhood.SaleCount, neighborhood.RentCount));
    }

    [Fact]
    public async Task Zone_Returns404_ForAnUnknownZoneOrOneWithNothingPublished()
    {
        var agency = await AddCompanyAsync();
        var city = Unique("Borrador");
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Sale, PropertyStatus.Draft);
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/zones/{SlugOf(city)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/public/zones/no-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/public/zones/no-existe/tampoco")).StatusCode);
    }

    [Fact]
    public async Task Zone_Returns404_ForANeighborhoodThatBelongsToAnotherCity()
    {
        var agency = await AddCompanyAsync();
        var one = Unique("Uno");
        var two = Unique("Dos");
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", one, "Solo Aqui");
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", two, "Otra");

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/public/zones/{SlugOf(two)}/solo-aqui")).StatusCode);
    }

    [Fact]
    public async Task TopZones_ListCitiesAndNeighborhoods_ByNumberOfProperties()
    {
        var agency = await AddCompanyAsync();
        var city = Unique("Popular");
        for (var i = 0; i < 3; i++)
        {
            await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Estrella");
        }

        var top = await (await factory.CreateClient().GetAsync("/api/public/zones?limit=100")).ReadAsync<List<ZoneLinkDto>>();

        var cityRow = Assert.Single(top, z => z.CitySlug == SlugOf(city) && z.NeighborhoodSlug is null);
        var neighborhoodRow = Assert.Single(top, z => z.CitySlug == SlugOf(city) && z.NeighborhoodSlug == "estrella");
        Assert.Equal(3, cityRow.SaleCount);
        Assert.Equal(3, neighborhoodRow.SaleCount);
        Assert.Equal("Estrella", neighborhoodRow.Name);
        Assert.Equal(city, neighborhoodRow.CityName);
        Assert.Equal(top.OrderByDescending(z => z.SaleCount + z.RentCount).Select(z => z.SaleCount + z.RentCount), top.Select(z => z.SaleCount + z.RentCount));
    }

    // --- Listado por zona -------------------------------------------------------------------

    [Fact]
    public async Task Catalog_FiltersByCityAndNeighborhoodSlug()
    {
        var agency = await AddCompanyAsync();
        var city = Unique("Filtro");
        var inCentro = await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Sale, title: "En Centro");
        var inNorte = await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Norte", OperationType.Sale, title: "En Norte");
        var rent = await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro", OperationType.Rent, title: "Renta Centro");
        await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", Unique("Otra"), "Centro", OperationType.Sale, title: "Otra ciudad");
        var anonymous = factory.CreateClient();
        var slug = SlugOf(city);

        async Task<List<Guid>> IdsAsync(string query) =>
            (await (await anonymous.GetAsync($"/api/public/properties?pageSize=100&{query}")).ReadAsync<PagedResult<PublicPropertyDto>>()).Items.Select(p => p.Id).ToList();

        var wholeCity = await IdsAsync($"citySlug={slug}");
        var sale = await IdsAsync($"citySlug={slug}&operation=Sale");
        var centro = await IdsAsync($"citySlug={slug}&neighborhoodSlug=centro");
        var centroSale = await IdsAsync($"citySlug={slug.ToUpperInvariant()}&neighborhoodSlug=CENTRO&operation=Sale");

        Assert.Equal(new[] { inCentro.Id, inNorte.Id, rent.Id }.Order(), wholeCity.Order());
        Assert.Equal(new[] { inCentro.Id, inNorte.Id }.Order(), sale.Order());
        Assert.Equal(new[] { inCentro.Id, rent.Id }.Order(), centro.Order());
        Assert.Equal([inCentro.Id], centroSale);
        Assert.Empty(await IdsAsync($"citySlug={slug}&neighborhoodSlug=no-existe"));
        Assert.Empty(await IdsAsync("citySlug=ciudad-que-no-existe"));
    }

    [Fact]
    public async Task PublicProperty_CarriesTheSlugsOfItsZonePages()
    {
        var agency = await AddCompanyAsync();
        var city = Unique("Detalle");
        var located = await factory.AddLocatedPropertyAsync(agency.Id, "Jalisco", city, "Centro Historico");
        var anonymous = factory.CreateClient();

        var detail = await (await anonymous.GetAsync($"/api/public/properties/{located.Id}")).ReadAsync<PublicPropertyDto>();
        var inList = (await (await anonymous.GetAsync($"/api/public/properties?citySlug={SlugOf(city)}")).ReadAsync<PagedResult<PublicPropertyDto>>()).Items.Single();

        Assert.Equal(SlugOf(city), detail.CitySlug);
        Assert.Equal("centro-historico", detail.NeighborhoodSlug);
        Assert.Equal(detail.CitySlug, inList.CitySlug);
    }

    [Fact]
    public async Task Catalog_RejectsANeighborhoodWithoutItsCity() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.CreateClient().GetAsync("/api/public/properties?neighborhoodSlug=centro")).StatusCode);

    // --- Mapa del sitio ---------------------------------------------------------------------

    [Fact]
    public async Task Sitemap_IsPublicXml_WithZonesListingsAndCompanies()
    {
        var company = await AddCompanyAsync();
        var city = Unique("Mapa");
        var listing = await factory.AddLocatedPropertyAsync(company.Id, "Jalisco", city, "Centro", OperationType.Sale);
        var draft = await factory.AddLocatedPropertyAsync(company.Id, "Jalisco", city, "Draft Only", OperationType.Rent, PropertyStatus.Draft);
        var individual = Agency.CreateIndividual("Ana Privada", $"{Guid.NewGuid():N}@individual.test");
        await factory.WithDbAsync(async db =>
        {
            db.Agencies.Add(individual);
            await db.SaveChangesAsync();
        });
        var privateListing = await factory.AddLocatedPropertyAsync(individual.Id, "Jalisco", city, "Centro", OperationType.Sale);

        var response = await factory.CreateClient().GetAsync("/api/public/sitemap.xml");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        var xml = await response.Content.ReadAsStringAsync();
        var locs = XDocument.Parse(xml).Descendants(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9")).Select(e => e.Value).ToList();
        const string site = "http://localhost:4200";
        Assert.Contains($"{site}/", locs);
        Assert.Contains($"{site}/venta/{SlugOf(city)}", locs);
        Assert.Contains($"{site}/venta/{SlugOf(city)}/centro", locs);
        Assert.Contains($"{site}/publicaciones/{listing.Id}", locs);
        Assert.Contains($"{site}/inmobiliarias/{company.Id}", locs);
        Assert.DoesNotContain($"{site}/renta/{SlugOf(city)}", locs);
        Assert.DoesNotContain($"{site}/publicaciones/{draft.Id}", locs);
        Assert.DoesNotContain($"{site}/inmobiliarias/{individual.Id}", locs);
        Assert.Contains($"{site}/publicaciones/{privateListing.Id}", locs);
    }

    // --- Enlazar lo que ya existía ----------------------------------------------------------

    [Fact]
    public async Task LinkingLocations_FillsInThePropertiesThatPredateTheCatalog_AndIsIdempotent()
    {
        var agency = await AddCompanyAsync();
        var city = Unique("Antigua");
        var legacy = await factory.AddPropertyAsync(agency.Id, city: city.ToUpperInvariant(), status: PropertyStatus.Published);

        await Link();
        await Link();

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == legacy.Id);
            Assert.NotNull(stored.CityId);
            Assert.NotNull(stored.StateId);
            Assert.Null(stored.NeighborhoodId);
            Assert.Equal("Ciudad de México", stored.State);
            Assert.Single(await db.GeoCities.Where(c => c.Slug == SlugOf(city)).ToListAsync());
            Assert.True(await db.GeoStates.CountAsync() >= 32);
        });

        async Task Link()
        {
            using var scope = factory.Services.CreateScope();
            await DatabaseInitializer.LinkLocationsAsync(scope.ServiceProvider);
        }
    }
}
