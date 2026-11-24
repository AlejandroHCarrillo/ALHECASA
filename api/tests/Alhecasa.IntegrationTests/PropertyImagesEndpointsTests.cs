using System.Net;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Imágenes de las propiedades: de 1 a 10, subida atómica, aislamiento por inmobiliaria y entrega pública.</summary>
public sealed class PropertyImagesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2];

    private async Task<(Agency Agency, User Seller)> CreateTenantAsync()
    {
        var agency = await factory.AddAgencyAsync();
        return (agency, await factory.AddUserAsync(UserRole.Seller, agency.Id));
    }

    private static string ImagesUrl(Guid propertyId) => $"/api/properties/{propertyId}/images";

    private static MultipartFormDataContent Form(IEnumerable<(byte[] Bytes, string Name, string Type)> files, IEnumerable<Guid>? remove = null)
    {
        var form = new MultipartFormDataContent();
        foreach (var (bytes, name, type) in files)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new(type);
            form.Add(content, "files", name);
        }

        foreach (var id in remove ?? [])
        {
            form.Add(new StringContent(id.ToString()), "removeImageIds");
        }

        return form;
    }

    private static (byte[], string, string) Jpeg(int n = 0) => (ApiFactory.JpegBytes, $"foto{n}.jpg", "image/jpeg");

    private static UpdatePropertyRequest Publish() => new()
    {
        Title = "Casa",
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Status = PropertyStatus.Published,
        Price = 1,
        City = "c",
        State = "s"
    };

    [Fact]
    public async Task Upload_StoresImages_AndReturnsThemInOrder()
    {
        var (agency, seller) = await CreateTenantAsync();
        var property = await factory.AddPropertyAsync(agency.Id);
        var client = factory.CreateClientFor(seller);

        var response = await client.PostAsync(ImagesUrl(property.Id), Form([Jpeg(1), (Png, "b.png", "image/png")]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.ReadAsync<PropertyDto>();
        Assert.Equal(2, dto.ImageIds.Count);
        var read = await (await client.GetAsync($"/api/properties/{property.Id}")).ReadAsync<PropertyDto>();
        Assert.Equal(dto.ImageIds, read.ImageIds);
    }

    [Fact]
    public async Task Upload_MoreThanTen_Returns400_AndKeepsTheGallery()
    {
        var (agency, seller) = await CreateTenantAsync();
        var property = await factory.AddPropertyAsync(agency.Id);
        await factory.AddImagesAsync(property.Id, 9);
        var client = factory.CreateClientFor(seller);

        var response = await client.PostAsync(ImagesUrl(property.Id), Form([Jpeg(1), Jpeg(2)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var read = await (await client.GetAsync($"/api/properties/{property.Id}")).ReadAsync<PropertyDto>();
        Assert.Equal(9, read.ImageIds.Count);
    }

    [Fact]
    public async Task Upload_CanReplaceInOneStep_WhenTheGalleryIsFull()
    {
        var (agency, seller) = await CreateTenantAsync();
        var property = await factory.AddPropertyAsync(agency.Id);
        var existing = await factory.AddImagesAsync(property.Id, 10);
        var client = factory.CreateClientFor(seller);

        var response = await client.PostAsync(ImagesUrl(property.Id), Form([Jpeg(1)], [existing[0].Id]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.ReadAsync<PropertyDto>();
        Assert.Equal(10, dto.ImageIds.Count);
        Assert.DoesNotContain(existing[0].Id, dto.ImageIds);
        Assert.False(factory.Storage.Contains(existing[0].StorageKey));
    }

    [Fact]
    public async Task Upload_ARenamedNonImage_Returns400_AndStoresNothing()
    {
        var (agency, seller) = await CreateTenantAsync();
        var property = await factory.AddPropertyAsync(agency.Id);
        var before = factory.Storage.Count;

        var response = await factory.CreateClientFor(seller)
            .PostAsync(ImagesUrl(property.Id), Form([(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "x.jpg", "image/jpeg")]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, factory.Storage.Count);
    }

    [Fact]
    public async Task Upload_InAnotherAgency_Returns404()
    {
        var (_, seller) = await CreateTenantAsync();
        var other = await factory.AddAgencyAsync("Otra");
        var foreign = await factory.AddPropertyAsync(other.Id);

        var response = await factory.CreateClientFor(seller).PostAsync(ImagesUrl(foreign.Id), Form([Jpeg()]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutToken_Returns401_AndAsClient_Returns403()
    {
        var (agency, _) = await CreateTenantAsync();
        var property = await factory.AddPropertyAsync(agency.Id);
        var client = await factory.AddUserAsync(UserRole.Client, agency.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsync(ImagesUrl(property.Id), Form([Jpeg()]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(client).PostAsync(ImagesUrl(property.Id), Form([Jpeg()]))).StatusCode);
    }

    [Fact]
    public async Task Publishing_NeedsAtLeastOneImage()
    {
        var (agency, seller) = await CreateTenantAsync();
        var property = await factory.AddPropertyAsync(agency.Id);
        var client = factory.CreateClientFor(seller);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutJsonAsync($"/api/properties/{property.Id}", Publish())).StatusCode);

        await client.PostAsync(ImagesUrl(property.Id), Form([Jpeg()]));
        Assert.Equal(HttpStatusCode.OK, (await client.PutJsonAsync($"/api/properties/{property.Id}", Publish())).StatusCode);
    }

    [Fact]
    public async Task CreatingAsPublished_Returns400_BecauseThereAreNoImagesYet()
    {
        var (_, seller) = await CreateTenantAsync();
        var request = new CreatePropertyRequest
        {
            Title = "Casa",
            Type = PropertyType.House,
            Operation = OperationType.Sale,
            Status = PropertyStatus.Published,
            Price = 1,
            City = "c",
            State = "s"
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await factory.CreateClientFor(seller).PostJsonAsync("/api/properties", request)).StatusCode);
    }

    [Fact]
    public async Task APublishedProperty_CannotLoseItsLastImage()
    {
        var (agency, seller) = await CreateTenantAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var images = await factory.AddImagesAsync(property.Id);

        var response = await factory.CreateClientFor(seller).PostAsync(ImagesUrl(property.Id), Form([], [images[0].Id]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Entrega pública --------------------------------------------------------------------

    [Fact]
    public async Task PublicList_ExposesImageIds_AndTheImageIsServedWithoutToken()
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, "Con fotos", status: PropertyStatus.Published);
        var images = await factory.AddImagesAsync(property.Id, 2);

        var page = await (await factory.CreateClient().GetAsync("/api/public/properties?pageSize=100")).ReadAsync<PagedResult<PublicPropertyDto>>();
        var listed = Assert.Single(page.Items, p => p.Id == property.Id);
        Assert.Equal(images.Select(i => i.Id), listed.ImageIds);

        var image = await factory.CreateClient().GetAsync($"/api/images/{images[0].Id}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ApiFactory.JpegBytes, await image.Content.ReadAsByteArrayAsync());
        Assert.Contains("immutable", image.Headers.CacheControl?.ToString());
        Assert.Contains("nosniff", image.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    public async Task Image_OfADeletedProperty_Returns404()
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var images = await factory.AddImagesAsync(property.Id);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == property.Id);
            stored.MarkAsDeleted(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/images/{images[0].Id}")).StatusCode);
    }

    [Fact]
    public async Task UnknownImage_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync($"/api/images/{Guid.NewGuid()}")).StatusCode);
    }
}
