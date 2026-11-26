using System.Net;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;

namespace Alhecasa.IntegrationTests;

/// <summary>Filtros avanzados y orden, en el catálogo público, en la galería y en la lista del personal (mismos criterios en los tres).</summary>
public sealed class PropertyAdvancedFiltersEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PublicUrl = "/api/public/properties";

    private static readonly string[] TerraceOnly = ["Terrace"];
    private static readonly string[] UnknownAmenity = ["Jacuzzi"];

    /// <summary>Un juego de propiedades en una ciudad única, para que cada prueba solo vea las suyas.</summary>
    private sealed record Fixture(Guid AgencyId, string City, Property Casa, Property Depto, Property Rentado, Property Terreno, Property Dolares);

    private async Task<Fixture> SeedAsync(PropertyStatus status = PropertyStatus.Published)
    {
        var agency = await factory.AddAgencyAsync();
        var city = $"Ciudad{Guid.NewGuid():N}"[..16];

        PropertyData Data(string title, PropertyType type, OperationType op, decimal price, CurrencyCode currency,
            int? bedrooms, int? bathrooms, decimal? construction, decimal? land, int? parking, bool pets, bool furnished, Amenity amenities) =>
            new(title, null, type, op, status, price, currency, null, null, city, "Estado", null, bedrooms, bathrooms, construction, land,
                ParkingSpaces: parking, PetsAllowed: pets, Furnished: furnished, Amenities: amenities);

        var casa = await factory.AddPropertyAsync(agency.Id, Data("Casa grande", PropertyType.House, OperationType.Sale, 5_000_000m, CurrencyCode.MXN,
            4, 3, 300m, 400m, 3, true, false, Amenity.Pool | Amenity.Garden | Amenity.Security24h));
        var depto = await factory.AddPropertyAsync(agency.Id, Data("Depto céntrico", PropertyType.Apartment, OperationType.Sale, 2_000_000m, CurrencyCode.MXN,
            2, 1, 80m, null, 1, false, false, Amenity.Elevator | Amenity.Gym));
        var rentado = await factory.AddPropertyAsync(agency.Id, Data("Depto amueblado en renta", PropertyType.Apartment, OperationType.Rent, 15_000m, CurrencyCode.MXN,
            1, 1, 50m, null, 0, true, true, Amenity.Elevator));
        var terreno = await factory.AddPropertyAsync(agency.Id, Data("Terreno", PropertyType.Land, OperationType.Sale, 1_000_000m, CurrencyCode.MXN,
            null, null, null, 900m, null, false, false, Amenity.None));
        var dolares = await factory.AddPropertyAsync(agency.Id, Data("Casa en dólares", PropertyType.House, OperationType.Sale, 250_000m, CurrencyCode.USD,
            3, 2, 200m, 250m, 2, false, false, Amenity.Pool));

        return new Fixture(agency.Id, city, casa, depto, rentado, terreno, dolares);
    }

    private async Task<List<string>> TitlesAsync(Fixture f, string query = "")
    {
        var response = await factory.CreateClient().GetAsync($"{PublicUrl}?pageSize=100&location={f.City}{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadAsync<PagedResult<PublicPropertyDto>>()).Items.Select(p => p.Title).ToList();
    }

    [Fact]
    public async Task Price_FiltersWithinTheGivenCurrency_AndNeverMixesCurrencies()
    {
        var f = await SeedAsync();

        Assert.Equal(["Casa grande"], await TitlesAsync(f, "&minPrice=3000000&currency=MXN"));
        Assert.Equal(["Depto amueblado en renta", "Depto céntrico", "Terreno"], (await TitlesAsync(f, "&maxPrice=2000000&currency=MXN")).Order());
        Assert.Equal(["Casa en dólares"], await TitlesAsync(f, "&minPrice=100000&maxPrice=300000&currency=USD"));
        Assert.Equal(["Depto céntrico"], await TitlesAsync(f, "&minPrice=1500000&maxPrice=2500000&currency=MXN"));
    }

    [Fact]
    public async Task MinimumBedroomsBathroomsAndParking_AreInclusive_AndExcludeUnknownValues()
    {
        var f = await SeedAsync();

        Assert.Equal(["Casa en dólares", "Casa grande"], (await TitlesAsync(f, "&minBedrooms=3")).Order());
        Assert.Equal(["Casa grande"], await TitlesAsync(f, "&minBedrooms=4"));
        Assert.Equal(["Casa en dólares", "Casa grande"], (await TitlesAsync(f, "&minBathrooms=2")).Order());
        Assert.Equal(["Casa en dólares", "Casa grande"], (await TitlesAsync(f, "&minParking=2")).Order());

        // El terreno no tiene recámaras: no cumple ningún mínimo.
        Assert.DoesNotContain("Terreno", await TitlesAsync(f, "&minBedrooms=1"));
    }

    [Fact]
    public async Task Area_UsesTheBuiltArea_AndTheLandAreaWhenThereIsNoConstruction()
    {
        var f = await SeedAsync();

        Assert.Equal(["Terreno"], await TitlesAsync(f, "&minArea=500"));
        Assert.Equal(["Casa en dólares", "Casa grande"], (await TitlesAsync(f, "&minArea=150&maxArea=400")).Order());
        Assert.Equal(["Depto amueblado en renta"], await TitlesAsync(f, "&maxArea=60"));
    }

    [Fact]
    public async Task PetsAllowedAndFurnished_OnlyNarrowWhenTrue()
    {
        var f = await SeedAsync();

        Assert.Equal(["Casa grande", "Depto amueblado en renta"], (await TitlesAsync(f, "&petsAllowed=true")).Order());
        Assert.Equal(["Depto amueblado en renta"], await TitlesAsync(f, "&furnished=true"));
        Assert.Equal(["Depto amueblado en renta"], await TitlesAsync(f, "&furnished=true&petsAllowed=true"));
        Assert.Equal(5, (await TitlesAsync(f, "&petsAllowed=false&furnished=false")).Count);
    }

    [Fact]
    public async Task Amenities_RequireAllTheOnesAsked()
    {
        var f = await SeedAsync();

        Assert.Equal(["Casa en dólares", "Casa grande"], (await TitlesAsync(f, "&amenities=Pool")).Order());
        Assert.Equal(["Casa grande"], await TitlesAsync(f, "&amenities=Pool&amenities=Garden"));
        Assert.Equal(["Depto céntrico"], await TitlesAsync(f, "&amenities=Elevator&amenities=Gym"));
        Assert.Empty(await TitlesAsync(f, "&amenities=Pool&amenities=Gym"));
        Assert.Equal(["Depto amueblado en renta", "Depto céntrico"], (await TitlesAsync(f, "&amenities=Elevator")).Order());
    }

    [Fact]
    public async Task Filters_CombineWithOperationAndType()
    {
        var f = await SeedAsync();

        Assert.Equal(["Depto amueblado en renta"], await TitlesAsync(f, "&operation=Rent&type=Apartment&furnished=true&minBedrooms=1"));
        Assert.Equal(["Casa grande"], await TitlesAsync(f, "&type=House&currency=MXN&maxPrice=6000000&amenities=Pool"));
    }

    [Fact]
    public async Task Sort_OrdersByPriceAscending()
    {
        var f = await SeedAsync();

        // El orden por precio compara el número tal cual, sin convertir monedas: 15 mil MXN, 250 mil USD, 1 M, 2 M y 5 M.
        Assert.Equal(
            ["Depto amueblado en renta", "Casa en dólares", "Terreno", "Depto céntrico", "Casa grande"],
            await TitlesAsync(f, "&sort=priceAsc"));
    }

    [Fact]
    public async Task Sort_OrdersByPriceDescending()
    {
        var f = await SeedAsync();

        Assert.Equal(
            ["Casa grande", "Depto céntrico", "Terreno", "Casa en dólares", "Depto amueblado en renta"],
            await TitlesAsync(f, "&sort=priceDesc"));
    }

    [Fact]
    public async Task Sort_OrdersByAreaDescending_UsingTheLandAreaWhenThereIsNoConstruction()
    {
        var f = await SeedAsync();

        Assert.Equal(
            ["Terreno", "Casa grande", "Casa en dólares", "Depto céntrico", "Depto amueblado en renta"],
            await TitlesAsync(f, "&sort=areaDesc"));
    }
    [Fact]
    public async Task Pagination_FollowsTheChosenOrder()
    {
        var f = await SeedAsync();

        var response = await factory.CreateClient().GetAsync($"{PublicUrl}?pageSize=2&page=3&location={f.City}&sort=priceAsc");
        var page = await response.ReadAsync<PagedResult<PublicPropertyDto>>();

        Assert.Equal(5, page.TotalCount);
        Assert.Equal("Casa grande", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task ThePublicDataIncludesTheNewFields_AndEveryFilterStillHidesUnpublishedProperties()
    {
        var f = await SeedAsync();
        var response = await factory.CreateClient().GetAsync($"{PublicUrl}?location={f.City}&amenities=Pool&minBedrooms=4");
        var item = Assert.Single((await response.ReadAsync<PagedResult<PublicPropertyDto>>()).Items);

        Assert.Equal(f.Casa.Id, item.Id);
        Assert.Equal(3, item.ParkingSpaces);
        Assert.True(item.PetsAllowed);
        Assert.False(item.Furnished);
        Assert.Equal([Amenity.Pool, Amenity.Security24h, Amenity.Garden], item.Amenities);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Pool\"", body, StringComparison.Ordinal);

    }

    [Fact]
    public async Task EveryFilter_StillHidesUnpublishedProperties()
    {
        var draft = await SeedAsync(PropertyStatus.Draft);

        Assert.Empty(await TitlesAsync(draft));
        Assert.Empty(await TitlesAsync(draft, "&amenities=Pool&minBedrooms=1&sort=priceAsc"));
    }
    [Theory]
    [InlineData("minPrice=100")]
    [InlineData("maxPrice=100")]
    [InlineData("minPrice=500&maxPrice=100&currency=MXN")]
    [InlineData("minPrice=-1&currency=MXN")]
    [InlineData("minArea=500&maxArea=100")]
    [InlineData("minArea=-5")]
    [InlineData("minBedrooms=-1")]
    [InlineData("minBedrooms=999")]
    [InlineData("minParking=-1")]
    [InlineData("currency=EUR")]
    [InlineData("currency=99")]
    [InlineData("sort=cheapest")]
    [InlineData("sort=99")]
    [InlineData("amenities=Jacuzzi")]
    [InlineData("amenities=3")]
    [InlineData("petsAllowed=quizas")]
    public async Task InvalidCriteria_Return400(string query)
    {
        var response = await factory.CreateClient().GetAsync($"{PublicUrl}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheGallery_AcceptsTheSameFilters_AndStaysInsideTheAgency()
    {
        var f = await SeedAsync();
        var other = await SeedAsync();
        var staff = await factory.AddUserAsync(UserRole.Seller, f.AgencyId);

        var response = await factory.CreateClientFor(staff).GetAsync("/api/properties/gallery?amenities=Pool&minBedrooms=4&sort=priceDesc");
        var page = await response.ReadAsync<PagedResult<PublicPropertyDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([f.Casa.Id], page.Items.Select(p => p.Id));
        Assert.DoesNotContain(page.Items, p => p.Id == other.Casa.Id);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await factory.CreateClientFor(staff).GetAsync("/api/properties/gallery?minPrice=5")).StatusCode);
    }

    [Fact]
    public async Task TheStaffList_FiltersByStatusTypeOperationPriceAndAmenities_AndNeverShowsAnotherAgency()
    {
        var agency = await factory.AddAgencyAsync();
        var city = $"Ciudad{Guid.NewGuid():N}"[..16];
        PropertyData Data(string title, PropertyStatus status, PropertyType type, decimal price, Amenity amenities) =>
            new(title, null, type, OperationType.Sale, status, price, CurrencyCode.MXN, null, null, city, "Estado", null, 2, 1, 90m, null,
                ParkingSpaces: 1, Amenities: amenities);

        var draft = await factory.AddPropertyAsync(agency.Id, Data("Borrador barato", PropertyStatus.Draft, PropertyType.House, 800_000m, Amenity.None));
        var published = await factory.AddPropertyAsync(agency.Id, Data("Publicada con alberca", PropertyStatus.Published, PropertyType.House, 3_000_000m, Amenity.Pool));
        var flat = await factory.AddPropertyAsync(agency.Id, Data("Depto pausado", PropertyStatus.Paused, PropertyType.Apartment, 1_500_000m, Amenity.Elevator));
        var other = await factory.AddAgencyAsync();
        await factory.AddPropertyAsync(other.Id, Data("De otra agencia", PropertyStatus.Published, PropertyType.House, 3_000_000m, Amenity.Pool));
        var staff = await factory.AddUserAsync(UserRole.Supervisor, agency.Id);
        var client = factory.CreateClientFor(staff);

        async Task<List<Guid>> IdsAsync(string query)
        {
            var response = await client.GetAsync($"/api/properties?pageSize=100&search={city}{query}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.ReadAsync<PagedResult<PropertyDto>>()).Items.Select(p => p.Id).ToList();
        }

        Assert.Equal([draft.Id], await IdsAsync("&status=Draft"));
        Assert.Equal([flat.Id], await IdsAsync("&type=Apartment"));
        Assert.Equal([published.Id], await IdsAsync("&amenities=Pool"));
        Assert.Equal([published.Id, flat.Id], await IdsAsync("&minPrice=1000000&currency=MXN&sort=priceDesc"));
        Assert.Equal([draft.Id, flat.Id, published.Id], await IdsAsync("&sort=priceAsc"));
        Assert.Equal(3, (await IdsAsync(string.Empty)).Count);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/properties?status=Archived")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/properties?maxPrice=10")).StatusCode);
    }

    [Fact]
    public async Task CreatingAndUpdating_SaveTheNewFields_AndRejectInvalidOnes()
    {
        var agency = await factory.AddAgencyAsync();
        var staff = await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id);
        var client = factory.CreateClientFor(staff);

        var body = new
        {
            title = "Con extras",
            type = "House",
            operation = "Rent",
            price = 20_000,
            currency = "MXN",
            city = "Puebla",
            state = "Puebla",
            parkingSpaces = 2,
            petsAllowed = true,
            furnished = true,
            amenities = new[] { "Pool", "Gym", "Pool" },
        };
        var created = await client.PostJsonAsync("/api/properties", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.ReadAsync<PropertyDto>();
        Assert.Equal(2, dto.ParkingSpaces);
        Assert.True(dto.PetsAllowed);
        Assert.True(dto.Furnished);
        Assert.Equal([Amenity.Pool, Amenity.Gym], dto.Amenities);

        var update = await client.PutJsonAsync($"/api/properties/{dto.Id}", new { body.title, body.type, body.operation, body.price, body.currency, body.city, body.state, amenities = TerraceOnly });
        var updated = await update.ReadAsync<PropertyDto>();
        Assert.Null(updated.ParkingSpaces);
        Assert.False(updated.PetsAllowed);
        Assert.Equal([Amenity.Terrace], updated.Amenities);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync("/api/properties", new { body.title, body.type, body.operation, body.price, body.currency, body.city, body.state, parkingSpaces = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync("/api/properties", new { body.title, body.type, body.operation, body.price, body.currency, body.city, body.state, parkingSpaces = 51 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostJsonAsync("/api/properties", new { body.title, body.type, body.operation, body.price, body.currency, body.city, body.state, amenities = UnknownAmenity })).StatusCode);
    }
}
