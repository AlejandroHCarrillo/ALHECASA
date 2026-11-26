using System.Net;
using Alhecasa.Application.Inquiries;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Solicitud de informes de un visitante sin sesión sobre una propiedad publicada.</summary>
public sealed class PublicInquiriesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Url(Guid propertyId) => $"/api/public/properties/{propertyId}/inquiries";

    private static CreateInquiryRequest ValidRequest() => new()
    {
        Name = "Ana López",
        Phone = "+52 55 1234 5678",
        Email = "Ana@Correo.com",
        Message = "¿Sigue disponible? ¿Aceptan mascotas?"
    };

    private async Task<int> CountInquiriesAsync(Guid propertyId)
    {
        var count = 0;
        await factory.WithDbAsync(async db => count = await db.Inquiries.IgnoreQueryFilters().CountAsync(i => i.PropertyId == propertyId));
        return count;
    }

    [Fact]
    public async Task AVisitorWithoutSession_SendsAnInquiry_ThatIsStoredForTheAgencyOfTheProperty()
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, "Casa con informes", status: PropertyStatus.Published);

        var response = await factory.CreateClient().PostJsonAsync(Url(property.Id), ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // El visitante no recibe nada del registro: no puede consultarlo después.
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Inquiries.IgnoreQueryFilters().SingleAsync(i => i.PropertyId == property.Id);
            Assert.Equal(agency.Id, stored.AgencyId);
            Assert.Equal("Casa con informes", stored.PropertyTitle);
            Assert.Equal("Ana López", stored.Name);
            Assert.Equal("+52 55 1234 5678", stored.Phone);
            Assert.Equal("ana@correo.com", stored.Email);
            Assert.Equal("¿Sigue disponible? ¿Aceptan mascotas?", stored.Message);
            Assert.Equal(InquiryStatus.New, stored.Status);
            Assert.True(stored.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        });
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Paused)]
    [InlineData(PropertyStatus.Closed)]
    public async Task APropertyThatIsNotPublished_Returns404_AndStoresNothing(PropertyStatus status)
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: status);

        var response = await factory.CreateClient().PostJsonAsync(Url(property.Id), ValidRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await CountInquiriesAsync(property.Id));
    }

    [Fact]
    public async Task AnUnknownProperty_Returns404()
    {
        var response = await factory.CreateClient().PostJsonAsync(Url(Guid.NewGuid()), ValidRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task APropertyOfADeletedAgency_Returns404()
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        await factory.ModifyAgencyAsync(agency.Id, a => a.MarkAsDeleted(DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().PostJsonAsync(Url(property.Id), ValidRequest())).StatusCode);
    }

    [Theory]
    [InlineData("", "5512345678", "a@a.com", "Hola")]
    [InlineData("Ana", "123", "a@a.com", "Hola")]
    [InlineData("Ana", "abcdefghij", "a@a.com", "Hola")]
    [InlineData("Ana", "5512345678", "no-es-correo", "Hola")]
    [InlineData("Ana", "5512345678", "a@a.com", "")]
    public async Task InvalidData_Returns400_AndStoresNothing(string name, string phone, string email, string message)
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var request = new CreateInquiryRequest { Name = name, Phone = phone, Email = email, Message = message };

        var response = await factory.CreateClient().PostJsonAsync(Url(property.Id), request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountInquiriesAsync(property.Id));
    }

    [Fact]
    public async Task ExtraFieldsInTheBody_CannotChooseTheAgencyNorTheStatus()
    {
        var agency = await factory.AddAgencyAsync();
        var other = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var body = new { name = "Ana", phone = "5512345678", email = "a@a.com", message = "Hola", agencyId = other.Id, status = "Closed" };

        var response = await factory.CreateClient().PostJsonAsync(Url(property.Id), body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Inquiries.IgnoreQueryFilters().SingleAsync(i => i.PropertyId == property.Id);
            Assert.Equal(agency.Id, stored.AgencyId);
            Assert.Equal(InquiryStatus.New, stored.Status);
        });
    }
}

/// <summary>El formulario público no puede usarse como buzón de spam: pocas solicitudes por IP.</summary>
public sealed class PublicInquiriesRateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task AfterTenRequestsFromTheSameIp_TheNextOneIsRejectedWith429()
    {
        var agency = await factory.AddAgencyAsync();
        var property = await factory.AddPropertyAsync(agency.Id, status: PropertyStatus.Published);
        var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Inquiries:PermitLimit", "10"));
        var client = limited.CreateClient();
        var request = new CreateInquiryRequest { Name = "Ana", Phone = "5512345678", Email = "a@a.com", Message = "Hola" };
        var url = $"/api/public/properties/{property.Id}/inquiries";

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostJsonAsync(url, request)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostJsonAsync(url, request)).StatusCode);
    }
}