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

    private PublicPropertyService CreateService() => new(_properties, new FakeImageStorage());

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
