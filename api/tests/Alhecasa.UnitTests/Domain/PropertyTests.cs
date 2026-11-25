using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.UnitTests.Domain;

public sealed class PropertyTests
{
    private static readonly Guid AgencyId = Guid.CreateVersion7();

    internal static PropertyData ValidData() => new(
        "Casa en Polanco",
        "Amplia, con jardín",
        PropertyType.House,
        OperationType.Sale,
        PropertyStatus.Draft,
        4_500_000m,
        CurrencyCode.MXN,
        "Calle Horacio 100",
        "Polanco",
        "Ciudad de México",
        "CDMX",
        "11560",
        3,
        2,
        250m,
        300m);

    [Fact]
    public void NewProperty_CopiesData_AndBelongsToTheAgency()
    {
        var property = new Property(AgencyId, ValidData());

        Assert.Equal(AgencyId, property.AgencyId);
        Assert.Equal("Casa en Polanco", property.Title);
        Assert.Equal(4_500_000m, property.Price);
        Assert.Equal(PropertyStatus.Draft, property.Status);
        Assert.Equal(7, property.Id.Version);
    }

    [Fact]
    public void Property_WithoutAgency_IsRejected()
    {
        Assert.Throws<DomainException>(() => new Property(Guid.Empty, ValidData()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Price_MustBePositive(int price)
    {
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { Price = price }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_IsRequired(string title)
    {
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { Title = title }));
    }

    [Fact]
    public void CityAndState_AreRequired()
    {
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { City = "" }));
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { State = " " }));
    }

    [Fact]
    public void NegativeRoomsOrAreas_AreRejected()
    {
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { Bedrooms = -1 }));
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { Bathrooms = -1 }));
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { ConstructionArea = -1 }));
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { LandArea = -1 }));
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    public void Coordinates_OutOfRange_AreRejected(double latitude, double longitude)
    {
        var data = ValidData() with { Latitude = (decimal)latitude, Longitude = (decimal)longitude };

        Assert.Throws<DomainException>(() => new Property(AgencyId, data));
    }

    [Fact]
    public void Coordinates_MustComeTogether()
    {
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { Latitude = 19.4m }));
        Assert.Throws<DomainException>(() => new Property(AgencyId, ValidData() with { Longitude = -99.1m }));
    }

    [Fact]
    public void Coordinates_AreStoredRoundedToSixDecimals_AndTheLimitsAreAccepted()
    {
        var property = new Property(AgencyId, ValidData() with { Latitude = 19.4326077m, Longitude = -99.1332079m });

        Assert.Equal(19.432608m, property.Latitude);
        Assert.Equal(-99.133208m, property.Longitude);

        property.SetCoordinates(90m, -180m);
        Assert.Equal(90m, property.Latitude);
        Assert.Equal(-180m, property.Longitude);
    }

    [Fact]
    public void SetCoordinates_CanRemoveTheLocation_ButNotLeaveItHalfSet()
    {
        var property = new Property(AgencyId, ValidData() with { Latitude = 19.4m, Longitude = -99.1m });

        Assert.Throws<DomainException>(() => property.SetCoordinates(19.4m, null));
        Assert.Equal(19.4m, property.Latitude);

        property.SetCoordinates(null, null);
        Assert.Null(property.Latitude);
        Assert.Null(property.Longitude);
    }

    [Fact]
    public void Update_WithoutCoordinates_RemovesTheLocation()
    {
        var property = new Property(AgencyId, ValidData() with { Latitude = 19.4m, Longitude = -99.1m });

        property.Update(ValidData());

        Assert.Null(property.Latitude);
        Assert.Null(property.Longitude);
    }

    [Fact]
    public void OptionalFields_CanBeOmitted()
    {
        var property = new Property(AgencyId, ValidData() with
        {
            Description = null,
            Street = null,
            Neighborhood = null,
            PostalCode = null,
            Bedrooms = null,
            Bathrooms = null,
            ConstructionArea = null,
            LandArea = null
        });

        Assert.Null(property.Bedrooms);
        Assert.Null(property.LandArea);
    }

    [Fact]
    public void Update_ChangesData_ButNotTheAgency()
    {
        var property = new Property(AgencyId, ValidData());

        property.Update(ValidData() with { Title = "Nuevo título", Status = PropertyStatus.Published, Price = 5_000_000m });

        Assert.Equal("Nuevo título", property.Title);
        Assert.Equal(PropertyStatus.Published, property.Status);
        Assert.Equal(5_000_000m, property.Price);
        Assert.Equal(AgencyId, property.AgencyId);
    }

    [Fact]
    public void Update_WithInvalidData_KeepsPreviousValues()
    {
        var property = new Property(AgencyId, ValidData());

        Assert.Throws<DomainException>(() => property.Update(ValidData() with { Title = "Otro", Price = 0 }));

        Assert.Equal("Casa en Polanco", property.Title);
        Assert.Equal(4_500_000m, property.Price);
    }

    [Fact]
    public void MarkAsDeleted_IsSoft()
    {
        var property = new Property(AgencyId, ValidData());

        property.MarkAsDeleted(DateTimeOffset.UtcNow);

        Assert.True(property.IsDeleted);
        Assert.NotNull(property.DeletedAt);
    }
}
