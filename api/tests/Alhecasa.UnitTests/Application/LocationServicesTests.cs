using System.Xml.Linq;
using Alhecasa.Application.Common;
using Alhecasa.Application.Locations;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Support;

namespace Alhecasa.UnitTests.Application;

/// <summary>Catálogo de ubicaciones: resolver lo que escribe la gente, sugerencias, zonas públicas y mapa del sitio.</summary>
public sealed class LocationServicesTests
{
    private readonly FakeLocationRepository _locations = new();
    private readonly FakePropertyRepository _properties = new();

    private LocationResolver Resolver() => new(_locations);

    // --- Resolver ---------------------------------------------------------------------------

    [Fact]
    public async Task Resolver_UnitesDifferentSpellings_IntoOneStateCityAndNeighborhood()
    {
        var first = await Resolver().ResolveAsync("CDMX", "Ciudad de México", "Polanco");
        var second = await Resolver().ResolveAsync("df", "ciudad de mexico", "  POLANCO ");

        Assert.Equal(first.StateId, second.StateId);
        Assert.Equal(first.CityId, second.CityId);
        Assert.Equal(first.NeighborhoodId, second.NeighborhoodId);
        Assert.Equal("Ciudad de México", second.StateName);
        Assert.Equal("Ciudad de México", second.CityName);
        Assert.Equal("Polanco", second.NeighborhoodName);
        Assert.Single(_locations.States);
        Assert.Single(_locations.Cities);
        Assert.Single(_locations.Neighborhoods);
    }

    [Fact]
    public async Task Resolver_FirstSpellingBecomesTheOfficialName()
    {
        await Resolver().ResolveAsync("Jalisco", "Guadalajara", "Providencia");

        var again = await Resolver().ResolveAsync("jal", "GUADALAJARA", "providencia");

        Assert.Equal("Guadalajara", again.CityName);
        Assert.Equal("Providencia", again.NeighborhoodName);
        Assert.Equal("Jalisco", again.StateName);
    }

    [Fact]
    public async Task Resolver_AcceptsAStateOutsideTheList_AsIs()
    {
        var result = await Resolver().ResolveAsync("Texas", "Austin", null);

        Assert.Equal("Texas", result.StateName);
        Assert.Equal("Austin", result.CityName);
        Assert.Null(result.NeighborhoodId);
        Assert.Null(result.NeighborhoodName);
    }

    [Fact]
    public async Task Resolver_TreatsABlankNeighborhoodAsNone()
    {
        var result = await Resolver().ResolveAsync("Jalisco", "Zapopan", "   ");

        Assert.Null(result.NeighborhoodId);
        Assert.Empty(_locations.Neighborhoods);
    }

    [Fact]
    public async Task Resolver_GivesTheSecondCityWithTheSameName_ASlugThatIncludesItsState()
    {
        var jalisco = await Resolver().ResolveAsync("Jalisco", "Santa Cruz", null);
        var oaxaca = await Resolver().ResolveAsync("Oaxaca", "Santa Cruz", null);

        Assert.NotEqual(jalisco.CityId, oaxaca.CityId);
        Assert.Equal(["santa-cruz", "santa-cruz-oaxaca"], _locations.Cities.Select(c => c.Slug).Order());
        Assert.Equal(oaxaca.CityId, (await Resolver().ResolveAsync("OAX", "santa cruz", null)).CityId);
        Assert.Equal(jalisco.CityId, (await Resolver().ResolveAsync("Jalisco", "santa cruz", null)).CityId);
    }

    [Fact]
    public async Task Resolver_RemembersWhatItCreatedBeforeSaving()
    {
        var resolver = Resolver();

        var a = await resolver.ResolveAsync("Nuevo León", "Monterrey", "Cumbres");
        var b = await resolver.ResolveAsync("NL", "monterrey", "cumbres");

        Assert.Equal(a.CityId, b.CityId);
        Assert.Equal(a.NeighborhoodId, b.NeighborhoodId);
    }

    [Theory]
    [InlineData("!!!", "Guadalajara", null)]
    [InlineData("Jalisco", "???", null)]
    [InlineData("Jalisco", "Guadalajara", "***")]
    public async Task Resolver_RejectsNamesWithoutLettersOrNumbers(string state, string city, string? neighborhood) =>
        await Assert.ThrowsAsync<DomainException>(() => Resolver().ResolveAsync(state, city, neighborhood));

    // --- Sugerencias ------------------------------------------------------------------------

    [Fact]
    public async Task States_AreTheMexicanOnesPlusTheCreated_WithoutRepeats()
    {
        await Resolver().ResolveAsync("Texas", "Austin", null);
        await Resolver().ResolveAsync("CDMX", "Ciudad de México", null);

        var states = await new LocationService(_locations).ListStatesAsync();

        Assert.Equal(33, states.Count);
        Assert.Contains("Texas", states);
        Assert.Single(states, s => s == "Ciudad de México");
        Assert.Equal(states.OrderBy(Slug.From, StringComparer.Ordinal), states);
    }

    [Fact]
    public async Task Cities_AreFilteredByStateAndText()
    {
        await Resolver().ResolveAsync("Jalisco", "Guadalajara", null);
        await Resolver().ResolveAsync("Jalisco", "Zapopan", null);
        await Resolver().ResolveAsync("Nuevo León", "Guadalupe", null);
        var service = new LocationService(_locations);

        Assert.Equal(["Guadalajara", "Zapopan"], await service.SearchCitiesAsync("jal", null));
        Assert.Equal(["Guadalajara"], await service.SearchCitiesAsync("Jalisco", "guada"));
        Assert.Equal(["Guadalajara", "Guadalupe", "Zapopan"], await service.SearchCitiesAsync(null, null));
        Assert.Empty(await service.SearchCitiesAsync("Yucatán", null));
    }

    [Fact]
    public async Task Neighborhoods_NeedAStateAndACity()
    {
        await Resolver().ResolveAsync("Jalisco", "Guadalajara", "Providencia");
        await Resolver().ResolveAsync("Jalisco", "Guadalajara", "Americana");
        var service = new LocationService(_locations);

        Assert.Equal(["Americana", "Providencia"], await service.SearchNeighborhoodsAsync("JAL", "guadalajara", null));
        Assert.Equal(["Providencia"], await service.SearchNeighborhoodsAsync("Jalisco", "Guadalajara", "prov"));
        Assert.Empty(await service.SearchNeighborhoodsAsync("Jalisco", null, null));
        Assert.Empty(await service.SearchNeighborhoodsAsync(null, "Guadalajara", null));
        Assert.Empty(await service.SearchNeighborhoodsAsync("Jalisco", "Zapopan", null));
    }

    // --- Zonas ------------------------------------------------------------------------------

    private void AddZone(string city, string? neighborhood, OperationType operation, int count, string state = "Ciudad de México") =>
        _properties.ZoneCounts.Add(new ZoneCount(state, city, Slug.From(city), neighborhood, neighborhood is null ? null : Slug.From(neighborhood), operation, count));

    private ZoneService Zones() => new(_properties);

    [Fact]
    public async Task Zone_OfACity_AddsUpItsNeighborhoodsAndThoseWithoutOne()
    {
        AddZone("Ciudad de México", "Polanco", OperationType.Sale, 3);
        AddZone("Ciudad de México", "Polanco", OperationType.Rent, 1);
        AddZone("Ciudad de México", "Roma Norte", OperationType.Sale, 2);
        AddZone("Ciudad de México", null, OperationType.Sale, 4);

        var zone = await Zones().GetAsync("ciudad-de-mexico", null);

        Assert.Equal("Ciudad de México", zone.CityName);
        Assert.Equal(9, zone.SaleCount);
        Assert.Equal(1, zone.RentCount);
        Assert.Null(zone.NeighborhoodName);
        Assert.Equal(["polanco", "roma-norte"], zone.Neighborhoods.Select(n => n.NeighborhoodSlug));
        Assert.Equal((3, 1), (zone.Neighborhoods[0].SaleCount, zone.Neighborhoods[0].RentCount));
    }

    [Fact]
    public async Task Zone_OfANeighborhood_CountsOnlyIt_AndKeepsTheSiblingList()
    {
        AddZone("Ciudad de México", "Polanco", OperationType.Sale, 3);
        AddZone("Ciudad de México", "Roma Norte", OperationType.Rent, 2);

        var zone = await Zones().GetAsync("Ciudad-de-Mexico", "ROMA-NORTE");

        Assert.Equal("Roma Norte", zone.NeighborhoodName);
        Assert.Equal("roma-norte", zone.NeighborhoodSlug);
        Assert.Equal((0, 2), (zone.SaleCount, zone.RentCount));
        Assert.Equal(2, zone.Neighborhoods.Count);
    }

    [Fact]
    public async Task Zone_NotFound_ForAnUnknownCityOrNeighborhood()
    {
        AddZone("Ciudad de México", "Polanco", OperationType.Sale, 3);
        var zones = Zones();

        await Assert.ThrowsAsync<NotFoundException>(() => zones.GetAsync("guadalajara", null));
        await Assert.ThrowsAsync<NotFoundException>(() => zones.GetAsync("ciudad-de-mexico", "roma"));
        await Assert.ThrowsAsync<NotFoundException>(() => zones.GetAsync("", null));
    }

    [Fact]
    public async Task TopZones_MixCitiesAndNeighborhoods_ByNumberOfProperties_AndAreCapped()
    {
        AddZone("Guadalajara", "Providencia", OperationType.Sale, 5, "Jalisco");
        AddZone("Guadalajara", "Americana", OperationType.Rent, 1, "Jalisco");
        AddZone("Monterrey", null, OperationType.Sale, 2, "Nuevo León");

        var top = await Zones().GetTopAsync(100);

        Assert.Equal(["Guadalajara", "Providencia", "Monterrey", "Americana"], top.Select(z => z.Name));
        Assert.Equal(6, top[0].SaleCount + top[0].RentCount);
        Assert.Single(await Zones().GetTopAsync(1));
        Assert.Single(await Zones().GetTopAsync(0));
        Assert.Equal(4, (await Zones().GetTopAsync(10_000)).Count);
    }

    // --- Mapa del sitio ---------------------------------------------------------------------

    private static List<string> Locs(string xml) =>
        XDocument.Parse(xml).Descendants(XName.Get("loc", "http://www.sitemaps.org/schemas/sitemap/0.9")).Select(e => e.Value).ToList();

    [Fact]
    public async Task Sitemap_ListsStaticPagesZonesAgenciesAndListings_WithTheConfiguredSite()
    {
        AddZone("Guadalajara", "Providencia", OperationType.Sale, 5, "Jalisco");
        AddZone("Guadalajara", null, OperationType.Rent, 1, "Jalisco");
        AddZone("Zapopan", null, OperationType.Sale, 0, "Jalisco");
        var company = Guid.CreateVersion7();
        var individual = Guid.CreateVersion7();
        var listing = Guid.CreateVersion7();
        _properties.SitemapProperties.Add(new SitemapProperty(listing, new DateTimeOffset(2026, 3, 4, 23, 0, 0, TimeSpan.Zero), company, false));
        _properties.SitemapProperties.Add(new SitemapProperty(Guid.CreateVersion7(), DateTimeOffset.UtcNow, individual, true));

        var xml = await new SitemapService(_properties, new SeoSettings("https://alhecasa.test/")).BuildAsync();
        var locs = Locs(xml);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml, StringComparison.Ordinal);
        Assert.Contains("https://alhecasa.test/", locs);
        Assert.Contains("https://alhecasa.test/calculadora-hipotecaria", locs);
        Assert.Contains("https://alhecasa.test/venta/guadalajara", locs);
        Assert.Contains("https://alhecasa.test/venta/guadalajara/providencia", locs);
        Assert.Contains("https://alhecasa.test/renta/guadalajara", locs);
        Assert.DoesNotContain("https://alhecasa.test/renta/guadalajara/providencia", locs);
        Assert.DoesNotContain("https://alhecasa.test/venta/zapopan", locs);
        Assert.Contains($"https://alhecasa.test/publicaciones/{listing}", locs);
        Assert.Contains($"https://alhecasa.test/inmobiliarias/{company}", locs);
        Assert.DoesNotContain($"https://alhecasa.test/inmobiliarias/{individual}", locs);
        Assert.Equal(locs.Count, locs.Distinct().Count());
        Assert.Contains("<lastmod>2026-03-04</lastmod>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sitemap_IsCappedAtTheProtocolLimit()
    {
        for (var index = 0; index < SitemapService.MaxUrls + 10; index++)
        {
            _properties.SitemapProperties.Add(new SitemapProperty(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), true));
        }

        var locs = Locs(await new SitemapService(_properties, new SeoSettings("https://alhecasa.test")).BuildAsync());

        Assert.Equal(SitemapService.MaxUrls, locs.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("alhecasa.com")]
    [InlineData("ftp://alhecasa.com")]
    [InlineData("javascript:alert(1)")]
    public void SeoSettings_RejectsAnAddressThatIsNotHttp(string site) =>
        Assert.Throws<InvalidOperationException>(() => new SeoSettings(site).NormalizedSiteUrl());

    [Theory]
    [InlineData("https://alhecasa.com", "https://alhecasa.com")]
    [InlineData("https://alhecasa.com/", "https://alhecasa.com")]
    [InlineData(" http://localhost:4200/ ", "http://localhost:4200")]
    [InlineData("https://alhecasa.com/?x=1#f", "https://alhecasa.com")]
    public void SeoSettings_NormalizesTheSiteAddress(string site, string expected) =>
        Assert.Equal(expected, new SeoSettings(site).NormalizedSiteUrl());
}
