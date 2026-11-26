using Alhecasa.Application.Agencies;
using Alhecasa.Application.Common;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.UnitTests.Support;
using FluentValidation;

namespace Alhecasa.UnitTests.Application;

public sealed class AgencyServiceTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 3];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2];

    private readonly FakeAgencyRepository _agencies = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeImageStorage _storage = new();
    private readonly Agency _agency;

    public AgencyServiceTests()
    {
        _agency = _agencies.Add("Inmobiliaria Norte");
    }

    private AgencyService CreateService(FakeCurrentUser user) =>
        new(_agencies, _unitOfWork, _storage, user, new UpdateAgencyProfileRequestValidator());

    private AgencyService AsAdmin() => CreateService(FakeCurrentUser.AgencyAdmin(_agency.Id));

    private static UpdateAgencyProfileRequest Request(Func<UpdateAgencyProfileRequest, UpdateAgencyProfileRequest>? change = null)
    {
        var request = new UpdateAgencyProfileRequest(
            "Inmobiliaria Norte SA",
            "contacto@norte.test",
            "55 1234 5678",
            "Más de 20 años vendiendo casas.",
            "https://norte.test",
            "https://facebook.com/norte",
            "https://instagram.com/norte",
            "Lun a Vie 9:00 a 18:00",
            "Av. Reforma 100",
            "Ciudad de México",
            "CDMX",
            "06600");
        return change is null ? request : change(request);
    }

    private static ImageUpload Image(byte[] bytes) => new(new MemoryStream(bytes), bytes.Length);

    // ---- Permisos ----

    [Fact]
    public async Task OnlyTheAgencyAdmin_CanReadOrEditTheProfile()
    {
        foreach (var user in new[]
                 {
                     FakeCurrentUser.PlatformAdmin(),
                     FakeCurrentUser.Client(),
                     new FakeCurrentUser { UserId = Guid.NewGuid(), Role = Alhecasa.Domain.Enums.UserRole.Seller, AgencyId = _agency.Id },
                     new FakeCurrentUser { UserId = Guid.NewGuid(), Role = Alhecasa.Domain.Enums.UserRole.Supervisor, AgencyId = _agency.Id }
                 })
        {
            var service = CreateService(user);
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetMineAsync());
            await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateMineAsync(Request()));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.SetLogoAsync(Image(Jpeg)));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.RemoveLogoAsync());
        }

        Assert.Equal("Inmobiliaria Norte", _agency.Name);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task AFreeIndividualAccount_HasNoAgencyProfile()
    {
        var individual = _agencies.AddIndividual();
        var service = CreateService(FakeCurrentUser.AgencyAdmin(individual.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetMineAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => service.UpdateMineAsync(Request()));
    }

    [Fact]
    public async Task AnAdminWhoseAgencyNoLongerExists_GetsNotFound()
    {
        var service = CreateService(FakeCurrentUser.AgencyAdmin(Guid.NewGuid()));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMineAsync());
    }

    // ---- Lectura y edición ----

    [Fact]
    public async Task GetMine_ReturnsTheProfileOfTheAdminsOwnAgency()
    {
        _agencies.Add("Otra");

        var profile = await AsAdmin().GetMineAsync();

        Assert.Equal(_agency.Id, profile.Id);
        Assert.Equal("Inmobiliaria Norte", profile.Name);
        Assert.False(profile.HasLogo);
    }

    [Fact]
    public async Task Update_ChangesEveryField_AndSaves()
    {
        var profile = await AsAdmin().UpdateMineAsync(Request());

        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal("Inmobiliaria Norte SA", _agency.Name);
        Assert.Equal("contacto@norte.test", _agency.Email);
        Assert.Equal("55 1234 5678", _agency.Phone);
        Assert.Equal("Más de 20 años vendiendo casas.", _agency.Description);
        Assert.Equal("https://norte.test", _agency.Website);
        Assert.Equal("https://facebook.com/norte", _agency.FacebookUrl);
        Assert.Equal("https://instagram.com/norte", _agency.InstagramUrl);
        Assert.Equal("Lun a Vie 9:00 a 18:00", _agency.OpeningHours);
        Assert.Equal("Av. Reforma 100", _agency.Street);
        Assert.Equal("Ciudad de México", _agency.City);
        Assert.Equal("CDMX", _agency.State);
        Assert.Equal("06600", _agency.PostalCode);
        Assert.Equal(_agency.Name, profile.Name);
        Assert.Equal(_agency.City, profile.City);
    }

    [Fact]
    public async Task Update_TrimsTexts_AndStoresBlanksAsNoData()
    {
        await AsAdmin().UpdateMineAsync(Request(r => r with
        {
            Name = "  Norte  ",
            Description = "   ",
            Website = "",
            Phone = null,
            Street = "  Calle 1  "
        }));

        Assert.Equal("Norte", _agency.Name);
        Assert.Null(_agency.Description);
        Assert.Null(_agency.Website);
        Assert.Null(_agency.Phone);
        Assert.Equal("Calle 1", _agency.Street);
    }

    [Fact]
    public async Task Update_OnlyOptionalFieldsMayBeEmpty()
    {
        await AsAdmin().UpdateMineAsync(Request(r => r with
        {
            Phone = null, Description = null, Website = null, FacebookUrl = null, InstagramUrl = null,
            OpeningHours = null, Street = null, City = null, State = null, PostalCode = null
        }));

        Assert.Null(_agency.City);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("email")]
    public async Task Update_RequiresNameAndEmail(string field)
    {
        var request = Request(r => field == "name" ? r with { Name = " " } : r with { Email = "" });

        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin().UpdateMineAsync(request));

        Assert.Equal("Inmobiliaria Norte", _agency.Name);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("no-es-un-correo")]
    [InlineData("a@")]
    public async Task Update_RejectsAnInvalidEmail(string email) =>
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin().UpdateMineAsync(Request(r => r with { Email = email })));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://norte.test")]
    [InlineData("norte.test")]
    [InlineData("https://")]
    [InlineData("//norte.test")]
    public async Task Update_RejectsWebAddressesThatAreNotHttp(string url)
    {
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin().UpdateMineAsync(Request(r => r with { Website = url })));
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin().UpdateMineAsync(Request(r => r with { FacebookUrl = url })));
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin().UpdateMineAsync(Request(r => r with { InstagramUrl = url })));
        Assert.Null(_agency.Website);
    }

    [Theory]
    [InlineData("http://norte.test")]
    [InlineData("https://www.norte.test/contacto?x=1")]
    public async Task Update_AcceptsHttpAndHttpsAddresses(string url)
    {
        await AsAdmin().UpdateMineAsync(Request(r => r with { Website = url }));

        Assert.Equal(url, _agency.Website);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("55<script>")]
    public async Task Update_RejectsAnInvalidPhone(string phone) =>
        await Assert.ThrowsAsync<ValidationException>(() => AsAdmin().UpdateMineAsync(Request(r => r with { Phone = phone })));

    [Fact]
    public async Task Update_EnforcesTheMaximumLengths()
    {
        var tooLong = new Dictionary<string, UpdateAgencyProfileRequest>
        {
            ["name"] = Request(r => r with { Name = new string('a', AgencyProfileLimits.Name + 1) }),
            ["description"] = Request(r => r with { Description = new string('a', AgencyProfileLimits.Description + 1) }),
            ["website"] = Request(r => r with { Website = "https://norte.test/" + new string('a', AgencyProfileLimits.Url) }),
            ["hours"] = Request(r => r with { OpeningHours = new string('a', AgencyProfileLimits.OpeningHours + 1) }),
            ["street"] = Request(r => r with { Street = new string('a', AgencyProfileLimits.Street + 1) }),
            ["city"] = Request(r => r with { City = new string('a', AgencyProfileLimits.City + 1) }),
            ["state"] = Request(r => r with { State = new string('a', AgencyProfileLimits.State + 1) }),
            ["postal"] = Request(r => r with { PostalCode = new string('1', AgencyProfileLimits.PostalCode + 1) })
        };

        foreach (var (field, request) in tooLong)
        {
            await Assert.ThrowsAsync<ValidationException>(() => AsAdmin().UpdateMineAsync(request));
            Assert.True(_agency.City is null, $"{field} was saved");
        }
    }

    // ---- Logotipo ----

    [Fact]
    public async Task SetLogo_StoresTheFile_AndMarksTheAgency()
    {
        var profile = await AsAdmin().SetLogoAsync(Image(Png));

        Assert.True(profile.HasLogo);
        Assert.True(_agency.HasLogo);
        Assert.EndsWith(".png", _agency.LogoStorageKey);
        Assert.Equal("image/png", _agency.LogoContentType);
        Assert.Equal(Png, _storage.Files[_agency.LogoStorageKey!]);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SetLogo_ReplacingIt_DeletesThePreviousFile()
    {
        await AsAdmin().SetLogoAsync(Image(Png));
        var first = _agency.LogoStorageKey!;

        await AsAdmin().SetLogoAsync(Image(Jpeg));

        Assert.NotEqual(first, _agency.LogoStorageKey);
        Assert.False(_storage.Files.ContainsKey(first));
        Assert.Single(_storage.Files);
        Assert.Equal("image/jpeg", _agency.LogoContentType);
    }

    [Fact]
    public async Task SetLogo_RejectsWhatIsNotAnImage_WithoutTouchingStorage()
    {
        var notAnImage = new byte[] { (byte)'<', (byte)'s', (byte)'v', (byte)'g', (byte)'>', 0, 0, 0, 0, 0, 0, 0 };

        await Assert.ThrowsAsync<DomainException>(() => AsAdmin().SetLogoAsync(Image(notAnImage)));
        await Assert.ThrowsAsync<DomainException>(() => AsAdmin().SetLogoAsync(Image([])));
        await Assert.ThrowsAsync<DomainException>(() =>
            AsAdmin().SetLogoAsync(new ImageUpload(new MemoryStream(Jpeg), PropertyImageRules.MaxBytes + 1)));

        Assert.False(_agency.HasLogo);
        Assert.Empty(_storage.Files);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RemoveLogo_DeletesTheFile_AndClearsTheAgency()
    {
        await AsAdmin().SetLogoAsync(Image(Png));
        var key = _agency.LogoStorageKey!;

        var profile = await AsAdmin().RemoveLogoAsync();

        Assert.False(profile.HasLogo);
        Assert.Null(_agency.LogoStorageKey);
        Assert.Null(_agency.LogoContentType);
        Assert.False(_storage.Files.ContainsKey(key));
    }

    [Fact]
    public async Task RemoveLogo_WithoutOne_DoesNothing()
    {
        var profile = await AsAdmin().RemoveLogoAsync();

        Assert.False(profile.HasLogo);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- Información pública ----

    private PublicAgencyService CreatePublic() => new(_agencies, _storage);

    [Fact]
    public async Task Public_ShowsTheConfiguredInformation()
    {
        await AsAdmin().UpdateMineAsync(Request());

        var dto = await CreatePublic().GetAsync(_agency.Id);

        Assert.Equal("Inmobiliaria Norte SA", dto.Name);
        Assert.Equal("https://norte.test", dto.Website);
        Assert.Equal("Lun a Vie 9:00 a 18:00", dto.OpeningHours);
        Assert.Equal("Av. Reforma 100", dto.Street);
    }

    [Fact]
    public async Task Public_NeverShowsAnIndividualAnInactiveOrADeletedAgency()
    {
        var individual = _agencies.AddIndividual("Ana López");
        var inactive = _agencies.Add("Inactiva");
        typeof(Agency).GetProperty(nameof(Agency.IsActive))!.SetValue(inactive, false);
        var deleted = _agencies.Add("Eliminada");
        deleted.MarkAsDeleted(DateTimeOffset.UtcNow);

        foreach (var id in new[] { individual.Id, inactive.Id, deleted.Id, Guid.NewGuid() })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => CreatePublic().GetAsync(id));
            Assert.Null(await CreatePublic().OpenLogoAsync(id));
        }
    }

    [Fact]
    public async Task Public_Logo_IsServedWithItsTypeAndAVersion()
    {
        await AsAdmin().SetLogoAsync(Image(Png));

        var logo = await CreatePublic().OpenLogoAsync(_agency.Id);

        Assert.NotNull(logo);
        Assert.Equal("image/png", logo.ContentType);
        Assert.Equal(_agency.LogoStorageKey, logo.ETag);
        using var buffer = new MemoryStream();
        await logo.Content.CopyToAsync(buffer);
        Assert.Equal(Png, buffer.ToArray());
    }

    [Fact]
    public async Task Public_Logo_IsNullWhenThereIsNone() =>
        Assert.Null(await CreatePublic().OpenLogoAsync(_agency.Id));
}