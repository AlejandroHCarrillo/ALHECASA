using System.Net;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;

namespace Alhecasa.IntegrationTests;

/// <summary>«Ver todas las publicaciones de esta inmobiliaria»: el catálogo público filtrado por inmobiliaria, sin delatar a los particulares.</summary>
public sealed class PublicAgencyListingsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/public/properties";

    private async Task<PagedResult<PublicPropertyDto>> ListAsync(string query = "")
    {
        var response = await factory.CreateClient().GetAsync($"{Url}?pageSize=100{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResult<PublicPropertyDto>>();
    }

    private async Task<Agency> AddIndividualAsync()
    {
        var agency = Agency.CreateIndividual("Ana López Real", $"{Guid.NewGuid():N}@individual.test");
        await factory.WithDbAsync(async db =>
        {
            db.Agencies.Add(agency);
            await db.SaveChangesAsync();
        });
        return agency;
    }

    [Fact]
    public async Task EachPublication_CarriesTheIdOfItsAgency_SoTheCardCanLinkToItsListings()
    {
        var agency = await factory.AddAgencyAsync("Agencia Norte");
        var property = await factory.AddPropertyAsync(agency.Id, "De la agencia", status: PropertyStatus.Published);

        var item = (await ListAsync()).Items.Single(p => p.Id == property.Id);
        var detail = await (await factory.CreateClient().GetAsync($"{Url}/{property.Id}")).ReadAsync<PublicPropertyDto>();

        Assert.Equal(agency.Id, item.AgencyId);
        Assert.Equal(agency.Id, detail.AgencyId);
    }

    [Fact]
    public async Task FilteringByAgency_ReturnsOnlyItsActivePublications()
    {
        var agency = await factory.AddAgencyAsync("Agencia Norte");
        var other = await factory.AddAgencyAsync("Agencia Sur");
        var mine1 = await factory.AddPropertyAsync(agency.Id, "Activa 1", status: PropertyStatus.Published);
        var mine2 = await factory.AddPropertyAsync(agency.Id, "Activa 2", status: PropertyStatus.Published);
        var draft = await factory.AddPropertyAsync(agency.Id, "Borrador", status: PropertyStatus.Draft);
        var paused = await factory.AddPropertyAsync(agency.Id, "Pausada", status: PropertyStatus.Paused);
        var closed = await factory.AddPropertyAsync(agency.Id, "Cerrada", status: PropertyStatus.Closed);
        var theirs = await factory.AddPropertyAsync(other.Id, "Ajena", status: PropertyStatus.Published);

        var page = await ListAsync($"&agencyId={agency.Id}");

        Assert.Equal([mine1.Id, mine2.Id], page.Items.Select(p => p.Id).Order());
        Assert.DoesNotContain(page.Items, p => p.Id == theirs.Id || p.Id == draft.Id || p.Id == paused.Id || p.Id == closed.Id);
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task FilteringByAnAgencyWithoutPublications_ReturnsAnEmptyPage_NotAnError()
    {
        var agency = await factory.AddAgencyAsync();
        await factory.AddPropertyAsync(agency.Id, "Borrador", status: PropertyStatus.Draft);

        Assert.Empty((await ListAsync($"&agencyId={agency.Id}")).Items);
        Assert.Empty((await ListAsync($"&agencyId={Guid.NewGuid()}")).Items);
    }

    [Fact]
    public async Task FilteringByAgency_CombinesWithTheOtherFilters()
    {
        var agency = await factory.AddAgencyAsync();
        var sale = await factory.AddPropertyAsync(agency.Id, "En venta", status: PropertyStatus.Published, operation: OperationType.Sale);
        var rent = await factory.AddPropertyAsync(agency.Id, "En renta", status: PropertyStatus.Published, operation: OperationType.Rent);

        var page = await ListAsync($"&agencyId={agency.Id}&operation=Rent");

        Assert.Equal([rent.Id], page.Items.Select(p => p.Id));
        Assert.DoesNotContain(page.Items, p => p.Id == sale.Id);
    }

    [Fact]
    public async Task AnIndividual_StaysAnonymous_NoIdIsPublished_AndItsSpaceCannotBeListedByIdEither()
    {
        var individual = await AddIndividualAsync();
        var property = await factory.AddPropertyAsync(individual.Id, "De un particular", status: PropertyStatus.Published);

        var item = (await ListAsync()).Items.Single(p => p.Id == property.Id);

        Assert.Equal("Particular", item.AgencyName);
        Assert.Null(item.AgencyId);
        Assert.Empty((await ListAsync($"&agencyId={individual.Id}")).Items);
    }

    [Fact]
    public async Task APropertyOfADeactivatedAgency_DoesNotAppearEvenWhenFilteringByIt()
    {
        var agency = await factory.AddAgencyAsync();
        await factory.AddPropertyAsync(agency.Id, "De agencia dada de baja", status: PropertyStatus.Published);
        await factory.ModifyAgencyAsync(agency.Id, a => a.MarkAsDeleted(DateTimeOffset.UtcNow));

        Assert.Empty((await ListAsync($"&agencyId={agency.Id}")).Items);
    }
}