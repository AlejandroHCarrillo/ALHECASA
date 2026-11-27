using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;

namespace Alhecasa.UnitTests.Domain;

public sealed class LocationCatalogTests
{
    // --- Slug -------------------------------------------------------------------------------

    [Theory]
    [InlineData("Ciudad de México", "ciudad-de-mexico")]
    [InlineData("  Polanco  ", "polanco")]
    [InlineData("COL. DEL VALLE", "col-del-valle")]
    [InlineData("Peña Pobre", "pena-pobre")]
    [InlineData("San Ángel - Inn", "san-angel-inn")]
    [InlineData("Lomas de Chapultepec (Sección 2)", "lomas-de-chapultepec-seccion-2")]
    [InlineData("--A__B--", "a-b")]
    public void Slug_RemovesAccentsCaseAndSymbols(string text, string expected) =>
        Assert.Equal(expected, Slug.From(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("¿?!")]
    public void Slug_IsEmpty_WhenThereIsNothingToIdentify(string? text) =>
        Assert.Equal(string.Empty, Slug.From(text));

    [Fact]
    public void Slug_IsTruncated_WithoutATrailingDash()
    {
        var slug = Slug.From(new string('a', 95) + " " + new string('b', 20));

        Assert.True(slug.Length <= Slug.MaxLength);
        Assert.DoesNotContain("--", slug, StringComparison.Ordinal);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public void Slug_DoesNotAllowUnsafeCharacters()
    {
        var slug = Slug.From("../../etc/passwd <script>alert(1)</script> %2e");

        Assert.Matches("^[a-z0-9-]+$", slug);
    }

    // --- Nombres ----------------------------------------------------------------------------

    [Theory]
    [InlineData("polanco", "Polanco")]
    [InlineData("  col.   del   valle ", "Col. del Valle")]
    [InlineData("COLONIA DEL VALLE", "Colonia del Valle")]
    [InlineData("Ciudad de México", "Ciudad de México")]
    [InlineData("CDMX", "CDMX")]
    [InlineData("NL", "NL")]
    [InlineData("Lomas de Chapultepec", "Lomas de Chapultepec")]
    [InlineData("san pedro garza garcía", "San Pedro Garza García")]
    public void Tidy_FixesSpacingAndUniformCapitalization(string text, string expected) =>
        Assert.Equal(expected, LocationNames.Tidy(text));

    // --- Estados ----------------------------------------------------------------------------

    [Theory]
    [InlineData("CDMX", "Ciudad de México")]
    [InlineData("cdmx", "Ciudad de México")]
    [InlineData("D.F.", "Ciudad de México")]
    [InlineData("ciudad de mexico", "Ciudad de México")]
    [InlineData("Edo. de México", "Estado de México")]
    [InlineData("EDOMEX", "Estado de México")]
    [InlineData("NL", "Nuevo León")]
    [InlineData("nuevo leon", "Nuevo León")]
    [InlineData("Qro.", "Querétaro")]
    [InlineData("jalisco", "Jalisco")]
    public void MexicanStates_RecognizesAbbreviationsAndAlternateNames(string text, string expected) =>
        Assert.Equal(expected, MexicanStates.Canonical(text));

    [Theory]
    [InlineData("Texas")]
    [InlineData("")]
    [InlineData(null)]
    public void MexicanStates_ReturnsNull_ForUnknownText(string? text) =>
        Assert.Null(MexicanStates.Canonical(text));

    [Fact]
    public void MexicanStates_ListsThe32States_WithoutRepeats()
    {
        Assert.Equal(32, MexicanStates.Names.Count);
        Assert.Equal(32, MexicanStates.Names.Select(Slug.From).Distinct().Count());
    }

    // --- Entidades --------------------------------------------------------------------------

    [Fact]
    public void State_TakesATidyNameAndItsSlug()
    {
        var state = new GeoState("  nuevo   león ");

        Assert.Equal("Nuevo León", state.Name);
        Assert.Equal("nuevo-leon", state.Slug);
        Assert.Equal(7, state.Id.Version);
    }

    [Fact]
    public void City_NeedsAState_AndAUsableName()
    {
        Assert.Throws<DomainException>(() => new GeoCity(Guid.Empty, "Guadalajara", "guadalajara"));
        Assert.Throws<DomainException>(() => new GeoCity(Guid.CreateVersion7(), " ", "x"));
        Assert.Throws<DomainException>(() => new GeoCity(Guid.CreateVersion7(), "!!!", "x"));
        Assert.Throws<DomainException>(() => new GeoCity(Guid.CreateVersion7(), "Guadalajara", " "));
        Assert.Throws<DomainException>(() => new GeoCity(Guid.CreateVersion7(), new string('a', GeoLimits.Name + 1), "x"));
    }

    [Fact]
    public void Neighborhood_NeedsACity_AndAUsableName()
    {
        Assert.Throws<DomainException>(() => new GeoNeighborhood(Guid.Empty, "Polanco"));
        Assert.Throws<DomainException>(() => new GeoNeighborhood(Guid.CreateVersion7(), ""));

        var neighborhood = new GeoNeighborhood(Guid.CreateVersion7(), "polanco");
        Assert.Equal("Polanco", neighborhood.Name);
        Assert.Equal("polanco", neighborhood.Slug);
    }

    // --- Propiedad --------------------------------------------------------------------------

    [Fact]
    public void Property_SetLocation_TakesTheOfficialNamesAndLinks()
    {
        var property = new Property(Guid.CreateVersion7(), PropertyTests.ValidData() with { City = "cdmx", State = "df", Neighborhood = "polanco" });
        var (stateId, cityId, neighborhoodId) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        property.SetLocation(stateId, "Ciudad de México", cityId, "Ciudad de México", neighborhoodId, "Polanco");

        Assert.Equal("Ciudad de México", property.State);
        Assert.Equal("Ciudad de México", property.City);
        Assert.Equal("Polanco", property.Neighborhood);
        Assert.Equal((stateId, cityId, neighborhoodId), (property.StateId!.Value, property.CityId!.Value, property.NeighborhoodId!.Value));
    }

    [Fact]
    public void Property_SetLocation_ClearsTheNeighborhood_WhenTheCatalogHasNone()
    {
        var property = new Property(Guid.CreateVersion7(), PropertyTests.ValidData() with { Neighborhood = "  " });

        property.SetLocation(Guid.CreateVersion7(), "Jalisco", Guid.CreateVersion7(), "Guadalajara", null, null);

        Assert.Null(property.Neighborhood);
        Assert.Null(property.NeighborhoodId);
    }
}
