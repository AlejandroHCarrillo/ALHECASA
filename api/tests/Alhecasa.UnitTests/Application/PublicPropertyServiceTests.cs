using Alhecasa.Application.Properties;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Domain;
using Alhecasa.UnitTests.Support;

namespace Alhecasa.UnitTests.Application;

public sealed class PublicPropertyServiceTests
{
    private readonly FakePropertyRepository _properties = new();

    private PublicPropertyService CreateService() => new(_properties, new FakeImageStorage(), TimeProvider.System);

    private Property Add(PropertyStatus status)
    {
        var property = new Property(Guid.CreateVersion7(), PropertyTests.ValidData() with { Status = status });
        _properties.Items.Add(property);
        return property;
    }

    [Fact]
    public void Normalize_RemovesDuplicateIds_AndTreatsAnEmptyListAsNoFilter()
    {
        var id = Guid.CreateVersion7();

        Assert.Equal([id], new PublicPropertyQuery(Ids: [id, id, id]).Normalize().Ids);
        Assert.Null(new PublicPropertyQuery(Ids: []).Normalize().Ids);
        Assert.Null(new PublicPropertyQuery().Normalize().Ids);
    }

    [Fact]
    public void Normalize_DropsDuplicateAmenities_AndTreatsAnEmptyListAsNoFilter()
    {
        var normalized = new PublicPropertyQuery(Amenities: [Amenity.Pool, Amenity.Pool, Amenity.Gym]).Normalize();

        Assert.Equal([Amenity.Pool, Amenity.Gym], normalized.Amenities);
        Assert.Null(new PublicPropertyQuery(Amenities: []).Normalize().Amenities);
    }

    [Fact]
    public void Normalize_AcceptsAValidCombinationOfCriteria()
    {
        var query = new PublicPropertyQuery(
            MinPrice: 1m, MaxPrice: 10m, Currency: CurrencyCode.USD, MinBedrooms: 0, MinBathrooms: 1, MinParking: 2,
            MinArea: 50m, MaxArea: 50m, PetsAllowed: true, Furnished: false, Sort: PropertySort.PriceDesc);

        Assert.Equal(query, query.Normalize());
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public void Normalize_RejectsInvalidCriteria(PublicPropertyQuery query) =>
        Assert.Throws<DomainException>(query.Normalize);

    public static TheoryData<PublicPropertyQuery> InvalidQueries => new()
    {
        new PublicPropertyQuery(MinPrice: 10m),
        new PublicPropertyQuery(MaxPrice: 10m),
        new PublicPropertyQuery(MinPrice: 10m, MaxPrice: 5m, Currency: CurrencyCode.MXN),
        new PublicPropertyQuery(MinPrice: -1m, Currency: CurrencyCode.MXN),
        new PublicPropertyQuery(MinArea: 10m, MaxArea: 5m),
        new PublicPropertyQuery(MinArea: -1m),
        new PublicPropertyQuery(MinBedrooms: -1),
        new PublicPropertyQuery(MinBathrooms: 51),
        new PublicPropertyQuery(MinParking: -1),
        new PublicPropertyQuery(Currency: (CurrencyCode)99),
        new PublicPropertyQuery(Sort: (PropertySort)99),
        new PublicPropertyQuery(Type: (PropertyType)99),
        new PublicPropertyQuery(Operation: (OperationType)99),
        new PublicPropertyQuery(Amenities: [(Amenity)3]),
        new PublicPropertyQuery(Amenities: [Amenity.None]),
    };

    [Fact]
    public async Task GetPublished_SendsTheNormalizedCriteriaToTheRepository()
    {
        await CreateService().GetPublishedAsync(new PublicPropertyQuery(Location: "  roma ", Page: 0, MinBedrooms: 2, Amenities: [Amenity.Pool, Amenity.Pool]));

        var sent = _properties.LastPublicQuery!;
        Assert.Equal("roma", sent.Location);
        Assert.Equal(1, sent.Page);
        Assert.Equal(2, sent.MinBedrooms);
        Assert.Equal([Amenity.Pool], sent.Amenities);
    }

    [Fact]
    public async Task PublicDto_ExposesTheNewDetails()
    {
        var property = new Property(Guid.CreateVersion7(), PropertyTests.ValidData() with
        {
            Status = PropertyStatus.Published,
            ParkingSpaces = 2,
            PetsAllowed = true,
            Furnished = true,
            Amenities = Amenity.Gym | Amenity.Pool
        });
        _properties.Items.Add(property);

        var dto = await CreateService().GetPublishedByIdAsync(property.Id);

        Assert.Equal(2, dto.ParkingSpaces);
        Assert.True(dto.PetsAllowed);
        Assert.True(dto.Furnished);
        Assert.Equal([Amenity.Pool, Amenity.Gym], dto.Amenities);
    }

    [Fact]
    public async Task PublicDto_ExposesOnlyAnApproximateLocation()
    {
        var property = new Property(Guid.CreateVersion7(), PropertyTests.ValidData() with { Status = PropertyStatus.Published, Latitude = 19.4326077m, Longitude = -99.1332079m });
        _properties.Items.Add(property);

        var dto = await CreateService().GetPublishedByIdAsync(property.Id);

        Assert.Equal(19.433m, dto.ApproximateLatitude);
        Assert.Equal(-99.133m, dto.ApproximateLongitude);
    }

    [Fact]
    public async Task PublicDto_WithoutLocation_HasNoCoordinates()
    {
        var property = Add(PropertyStatus.Published);

        var dto = await CreateService().GetPublishedByIdAsync(property.Id);

        Assert.Null(dto.ApproximateLatitude);
        Assert.Null(dto.ApproximateLongitude);
    }

    [Fact]
    public async Task GetPublished_WithIds_ReturnsOnlyThoseProperties()
    {
        var wanted = Add(PropertyStatus.Published);
        Add(PropertyStatus.Published);

        var page = await CreateService().GetPublishedAsync(new PublicPropertyQuery(Ids: [wanted.Id]));

        Assert.Equal([wanted.Id], page.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task GetPublished_WithIds_NeverRevealsAnUnpublishedProperty()
    {
        var draft = Add(PropertyStatus.Draft);

        var page = await CreateService().GetPublishedAsync(new PublicPropertyQuery(Ids: [draft.Id]));

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetPublished_AcceptsExactlyTheMaximumNumberOfIds_AndRejectsOneMore()
    {
        var service = CreateService();
        var ids = Enumerable.Range(0, PublicPropertyQuery.MaxIds + 1).Select(_ => Guid.CreateVersion7()).ToList();

        await service.GetPublishedAsync(new PublicPropertyQuery(Ids: ids.Take(PublicPropertyQuery.MaxIds).ToList()));
        await Assert.ThrowsAsync<DomainException>(() => service.GetPublishedAsync(new PublicPropertyQuery(Ids: ids)));
    }

    [Fact]
    public async Task GetPublished_CountsRepeatedIdsOnce_BeforeApplyingTheLimit()
    {
        var id = Guid.CreateVersion7();
        var repeated = Enumerable.Repeat(id, PublicPropertyQuery.MaxIds + 50).ToList();

        var page = await CreateService().GetPublishedAsync(new PublicPropertyQuery(Ids: repeated));

        Assert.Empty(page.Items);
    }
}
