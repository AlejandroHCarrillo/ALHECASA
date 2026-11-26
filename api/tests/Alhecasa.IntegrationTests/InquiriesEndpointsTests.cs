using System.Net;
using Alhecasa.Application.Common;
using Alhecasa.Application.Inquiries;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Alhecasa.IntegrationTests;

/// <summary>Bandeja de solicitudes de informes: solo personal de la inmobiliaria dueña, con respuesta y seguimiento.</summary>
public sealed class InquiriesEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/inquiries";

    private sealed record Tenant(Agency Agency, Property Property, User Admin, User Seller);

    private async Task<Tenant> CreateTenantAsync(string name = "Agency")
    {
        var agency = await factory.AddAgencyAsync(name);
        var property = await factory.AddPropertyAsync(agency.Id, $"Casa de {name}", status: PropertyStatus.Published);
        return new Tenant(
            agency,
            property,
            await factory.AddUserAsync(UserRole.AgencyAdmin, agency.Id),
            await factory.AddUserAsync(UserRole.Seller, agency.Id));
    }

    private Task<PagedResult<InquiryDto>> ListAsync(User user, string query = "") =>
        factory.CreateClientFor(user).GetAsync($"{Url}?pageSize=100{query}").ContinueWith(t => t.Result.ReadAsync<PagedResult<InquiryDto>>()).Unwrap();

    // --- Acceso -----------------------------------------------------------------------------

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url)).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.PlatformAdmin)]
    [InlineData(UserRole.Client)]
    public async Task PlatformAdminAndClients_Return403(UserRole role)
    {
        var tenant = await CreateTenantAsync();
        var user = await factory.AddUserAsync(role, role == UserRole.PlatformAdmin ? null : tenant.Agency.Id);
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id);
        var client = factory.CreateClientFor(user);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Url}/{inquiry.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostJsonAsync($"{Url}/{inquiry.Id}/follow-ups", new AddFollowUpRequest { Text = "Hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutJsonAsync($"{Url}/{inquiry.Id}/status", new ChangeInquiryStatusRequest { Status = InquiryStatus.Closed })).StatusCode);
    }

    // --- Lista y detalle --------------------------------------------------------------------

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyStaff_SeeTheProspectDataOfTheirOwnAgencyOnly(UserRole role)
    {
        var mine = await CreateTenantAsync("Mia");
        var other = await CreateTenantAsync("Otra");
        var own = await factory.AddInquiryAsync(mine.Agency.Id, mine.Property.Id, "Prospecto Mío", "Casa de Mia");
        await factory.AddInquiryAsync(other.Agency.Id, other.Property.Id, "Prospecto Ajeno");
        var staff = await factory.AddUserAsync(role, mine.Agency.Id);

        var page = await ListAsync(staff);

        var item = Assert.Single(page.Items);
        Assert.Equal(own.Id, item.Id);
        Assert.Equal("Prospecto Mío", item.Name);
        Assert.Equal("5512345678", item.Phone);
        Assert.Equal(own.Email, item.Email);
        Assert.Equal("¿Sigue disponible?", item.Message);
        Assert.Equal("Casa de Mia", item.PropertyTitle);
        Assert.Equal(mine.Property.Id, item.PropertyId);
        Assert.Equal(InquiryStatus.New, item.Status);
        Assert.Empty(item.FollowUps);
    }

    [Fact]
    public async Task AnInquiryOfAnotherAgency_Is404_ForDetailFollowUpAndStatus()
    {
        var mine = await CreateTenantAsync("Mia");
        var other = await CreateTenantAsync("Otra");
        var foreign = await factory.AddInquiryAsync(other.Agency.Id, other.Property.Id);
        var client = factory.CreateClientFor(mine.Admin);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{foreign.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostJsonAsync($"{Url}/{foreign.Id}/follow-ups", new AddFollowUpRequest { Text = "Hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutJsonAsync($"{Url}/{foreign.Id}/status", new ChangeInquiryStatusRequest { Status = InquiryStatus.Closed })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task List_FiltersByStatus_AndSearchesProspectAndProperty_AndEscapesWildcards()
    {
        var tenant = await CreateTenantAsync();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var fresh = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id, $"Lucía {marker}");
        var closed = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id, "Pedro", $"Depto {marker}");
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Inquiries.IgnoreQueryFilters().SingleAsync(i => i.Id == closed.Id);
            stored.ChangeStatus(InquiryStatus.Closed);
            await db.SaveChangesAsync();
        });

        Assert.Equal([closed.Id], (await ListAsync(tenant.Admin, "&status=Closed")).Items.Select(i => i.Id));
        Assert.Equal([fresh.Id], (await ListAsync(tenant.Admin, $"&search=lucía {marker}")).Items.Select(i => i.Id));
        Assert.Equal([closed.Id], (await ListAsync(tenant.Admin, $"&search=depto {marker}")).Items.Select(i => i.Id));
        Assert.Empty((await ListAsync(tenant.Admin, "&search=%25")).Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.CreateClientFor(tenant.Admin).GetAsync($"{Url}?status=Bogus")).StatusCode);
    }

    [Fact]
    public async Task List_IsPaged_NewestFirst()
    {
        var tenant = await CreateTenantAsync();
        for (var i = 0; i < 3; i++)
        {
            await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id, $"Prospecto {i}");
        }

        var response = await factory.CreateClientFor(tenant.Admin).GetAsync($"{Url}?page=2&pageSize=2");
        var page = await response.ReadAsync<PagedResult<InquiryDto>>();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal("Prospecto 0", Assert.Single(page.Items).Name);
    }

    // --- Respuesta y seguimiento ------------------------------------------------------------

    [Fact]
    public async Task AReply_IsSignedByTheStaffMember_MovesTheInquiryToInProgress_AndPersists()
    {
        var tenant = await CreateTenantAsync();
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id);
        var client = factory.CreateClientFor(tenant.Seller);

        var response = await client.PostJsonAsync($"{Url}/{inquiry.Id}/follow-ups", new AddFollowUpRequest { Kind = FollowUpKind.Reply, Text = "Claro, puede visitarla el sábado." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<InquiryDto>();
        Assert.Equal(InquiryStatus.InProgress, updated.Status);
        var entry = Assert.Single(updated.FollowUps);
        Assert.Equal(FollowUpKind.Reply, entry.Kind);
        Assert.Equal("Claro, puede visitarla el sábado.", entry.Text);
        Assert.Equal(tenant.Seller.FullName, entry.AuthorName);

        var reloaded = await (await client.GetAsync($"{Url}/{inquiry.Id}")).ReadAsync<InquiryDto>();
        Assert.Equal(InquiryStatus.InProgress, reloaded.Status);
        Assert.Equal(["Claro, puede visitarla el sábado."], reloaded.FollowUps.Select(f => f.Text));
    }

    [Fact]
    public async Task ColleaguesSeeEachOthersFollowUps_InOrder()
    {
        var tenant = await CreateTenantAsync();
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id);

        await factory.CreateClientFor(tenant.Seller).PostJsonAsync($"{Url}/{inquiry.Id}/follow-ups", new AddFollowUpRequest { Text = "Primera respuesta" });
        await factory.CreateClientFor(tenant.Admin).PostJsonAsync($"{Url}/{inquiry.Id}/follow-ups", new AddFollowUpRequest { Kind = FollowUpKind.Note, Text = "Nota del administrador" });

        var detail = await (await factory.CreateClientFor(tenant.Seller).GetAsync($"{Url}/{inquiry.Id}")).ReadAsync<InquiryDto>();

        Assert.Equal(["Primera respuesta", "Nota del administrador"], detail.FollowUps.Select(f => f.Text));
        Assert.Equal([tenant.Seller.FullName, tenant.Admin.FullName], detail.FollowUps.Select(f => f.AuthorName));
    }

    [Fact]
    public async Task ANote_DoesNotChangeTheStatus()
    {
        var tenant = await CreateTenantAsync();
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id);

        var updated = await (await factory.CreateClientFor(tenant.Seller)
            .PostJsonAsync($"{Url}/{inquiry.Id}/follow-ups", new AddFollowUpRequest { Kind = FollowUpKind.Note, Text = "Llamar mañana" })).ReadAsync<InquiryDto>();

        Assert.Equal(InquiryStatus.New, updated.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AFollowUpWithoutText_Returns400(string text)
    {
        var tenant = await CreateTenantAsync();
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id);

        var response = await factory.CreateClientFor(tenant.Seller).PostJsonAsync($"{Url}/{inquiry.Id}/follow-ups", new AddFollowUpRequest { Text = text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_ClosesAndReopens()
    {
        var tenant = await CreateTenantAsync();
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id);
        var client = factory.CreateClientFor(tenant.Seller);

        var closed = await (await client.PutJsonAsync($"{Url}/{inquiry.Id}/status", new ChangeInquiryStatusRequest { Status = InquiryStatus.Closed })).ReadAsync<InquiryDto>();
        Assert.Equal(InquiryStatus.Closed, closed.Status);

        var reopened = await (await client.PutJsonAsync($"{Url}/{inquiry.Id}/status", new ChangeInquiryStatusRequest { Status = InquiryStatus.InProgress })).ReadAsync<InquiryDto>();
        Assert.Equal(InquiryStatus.InProgress, reopened.Status);
        Assert.NotNull(reopened.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatus_WithAnUnknownValue_Returns400()
    {
        var tenant = await CreateTenantAsync();
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id);

        var response = await factory.CreateClientFor(tenant.Seller).PutAsync($"{Url}/{inquiry.Id}/status", JsonContent.Create(new { status = 99 }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheInquiryOfADeletedProperty_StaysInTheTray_WithTheTitleItHad()
    {
        var tenant = await CreateTenantAsync();
        var inquiry = await factory.AddInquiryAsync(tenant.Agency.Id, tenant.Property.Id, propertyTitle: "Casa que se borró");
        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Properties.IgnoreQueryFilters().SingleAsync(p => p.Id == tenant.Property.Id);
            stored.MarkAsDeleted(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });

        var detail = await (await factory.CreateClientFor(tenant.Seller).GetAsync($"{Url}/{inquiry.Id}")).ReadAsync<InquiryDto>();

        Assert.Equal("Casa que se borró", detail.PropertyTitle);
    }
}