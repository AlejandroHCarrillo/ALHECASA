using Alhecasa.Application.Abstractions;
using Alhecasa.Application.Billing;
using Alhecasa.Application.Common;
using Alhecasa.Application.Locations;
using Alhecasa.Application.Properties;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Domain;
using Alhecasa.UnitTests.Support;
using FluentValidation;

namespace Alhecasa.UnitTests.Application;

public sealed class PropertyServiceTests
{
    private readonly FakePropertyRepository _properties = new();
    private readonly FakeAgencyRepository _agencies = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeImageStorage _storage = new();
    private readonly FakePlanRepository _plans = new();
    private readonly FakeLocationRepository _locations = new();
    private readonly FakeSubscriptionRepository _subscriptions = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly Agency _agencyA;
    private readonly Agency _agencyB;

    public PropertyServiceTests()
    {
        _agencyA = _agencies.Add("Agency A");
        _agencyB = _agencies.Add("Agency B");
    }

    private PropertyService CreateService(ICurrentUser currentUser) => new(
        _properties,
        _agencies,
        _storage,
        _unitOfWork,
        currentUser,
        new CreatePropertyRequestValidator(),
        new UpdatePropertyRequestValidator(),
        _clock,
        new EntitlementService(_subscriptions, _plans, _properties, _clock, new AccountLimits(3)),
        new LocationResolver(_locations));

    private static FakeCurrentUser Staff(UserRole role, Guid agencyId) =>
        new() { UserId = Guid.CreateVersion7(), Role = role, AgencyId = agencyId };

    private static CreatePropertyRequest NewRequest(Guid? agencyId = null) => new()
    {
        AgencyId = agencyId,
        Title = "Casa en Polanco",
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Price = 4_500_000m,
        City = "Ciudad de México",
        State = "CDMX"
    };

    private Property AddProperty(Guid agencyId, string title = "Existente")
    {
        var property = new Property(agencyId, PropertyTests.ValidData() with { Title = title });
        _properties.Items.Add(property);
        return property;
    }

    // --- Quién puede administrar propiedades ------------------------------------------------

    [Fact]
    public async Task Client_CannotUseTheInventory()
    {
        var service = CreateService(FakeCurrentUser.Client());

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPagedAsync(new PropertyListQuery()));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(NewRequest()));
    }

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyStaff_CreatesProperty_InOwnAgency_EvenWithoutSendingAgencyId(UserRole role)
    {
        var service = CreateService(Staff(role, _agencyA.Id));

        var created = await service.CreateAsync(NewRequest());

        Assert.Equal(_agencyA.Id, created.AgencyId);
        Assert.Equal(PropertyStatus.Draft, created.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AgencyStaff_CannotCreate_ForAnotherAgency()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(NewRequest(_agencyB.Id)));
        Assert.Empty(_properties.Items);
    }

    [Fact]
    public async Task AgencyStaff_SendingTheirOwnAgencyId_IsAccepted()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        var created = await service.CreateAsync(NewRequest(_agencyA.Id));

        Assert.Equal(_agencyA.Id, created.AgencyId);
    }

    [Fact]
    public async Task PlatformAdmin_MustChooseAnAgency()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(NewRequest()));

        var created = await service.CreateAsync(NewRequest(_agencyB.Id));
        Assert.Equal(_agencyB.Id, created.AgencyId);
    }

    [Fact]
    public async Task PlatformAdmin_CreatingForUnknownAgency_IsNotFound()
    {
        var service = CreateService(FakeCurrentUser.PlatformAdmin());

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(NewRequest(Guid.CreateVersion7())));
    }

    // --- Validación -------------------------------------------------------------------------

    [Fact]
    public async Task Create_TrimsTextAndTurnsBlankOptionalsIntoNull()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var request = NewRequest() with { Title = "  Casa  ", Description = "   ", Street = "", City = " CDMX " };

        var created = await service.CreateAsync(request);

        Assert.Equal("Casa", created.Title);
        Assert.Null(created.Description);
        Assert.Null(created.Street);
        Assert.Equal("CDMX", created.City);
    }

    [Fact]
    public async Task Create_WithInvalidData_FailsValidation_AndSavesNothing()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var request = NewRequest() with { Title = "", Price = 0, City = "", Type = 0 };

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request));

        Assert.Contains(error.Errors, e => e.PropertyName == nameof(PropertyRequest.Title));
        Assert.Contains(error.Errors, e => e.PropertyName == nameof(PropertyRequest.Price));
        Assert.Contains(error.Errors, e => e.PropertyName == nameof(PropertyRequest.City));
        Assert.Contains(error.Errors, e => e.PropertyName == nameof(PropertyRequest.Type));
        Assert.Empty(_properties.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithTooLongTitle_FailsValidation()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(NewRequest() with { Title = new string('x', 151) }));
    }

    [Fact]
    public async Task Create_WithPriceBeyondColumnPrecision_FailsValidation()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(NewRequest() with { Price = PropertyRequestValidator<CreatePropertyRequest>.MaxPrice + 1 }));
    }

    // --- Consulta, modificación y baja ------------------------------------------------------

    [Fact]
    public async Task GetById_OfMissingProperty_IsNotFound()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task Update_ChangesFields_AndKeepsTheAgency()
    {
        var existing = AddProperty(_agencyA.Id);
        existing.AddImage("a".PadRight(32, 'a') + ".jpg", "image/jpeg", 100);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var request = new UpdatePropertyRequest
        {
            Title = "Actualizada",
            Type = PropertyType.Apartment,
            Operation = OperationType.Rent,
            Status = PropertyStatus.Published,
            Price = 25_000m,
            Currency = CurrencyCode.USD,
            City = "Monterrey",
            State = "NL"
        };

        var updated = await service.UpdateAsync(existing.Id, request);

        Assert.Equal("Actualizada", updated.Title);
        Assert.Equal(PropertyStatus.Published, updated.Status);
        Assert.Equal(CurrencyCode.USD, updated.Currency);
        Assert.Equal(_agencyA.Id, updated.AgencyId);
    }

    [Fact]
    public async Task Update_OfMissingProperty_IsNotFound()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(
            Guid.CreateVersion7(),
            new UpdatePropertyRequest { Title = "x", Type = PropertyType.House, Operation = OperationType.Sale, Price = 1, City = "c", State = "s" }));
    }

    [Fact]
    public async Task Delete_MarksPropertyAsSoftDeleted()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Supervisor, _agencyA.Id));

        await service.DeleteAsync(existing.Id);

        Assert.True(existing.IsDeleted);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(existing.Id));
    }

    // --- Imágenes ---------------------------------------------------------------------------

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 3];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2];

    private static readonly byte[] Webp =
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1, 2];

    private static ImageUpload Upload(byte[] bytes) => new(new MemoryStream(bytes), bytes.Length);

    private static UpdatePropertyRequest PublishedUpdate() => new()
    {
        Title = "x",
        Type = PropertyType.House,
        Operation = OperationType.Sale,
        Status = PropertyStatus.Published,
        Price = 1,
        City = "c",
        State = "s"
    };

    [Fact]
    public async Task Create_AsPublished_IsRejected_BecauseItHasNoImagesYet()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(NewRequest() with { Status = PropertyStatus.Published }));
        Assert.Empty(_properties.Items);
    }

    [Fact]
    public async Task Update_ToPublished_WithoutImages_IsRejected()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(existing.Id, PublishedUpdate()));
        Assert.Equal(PropertyStatus.Draft, existing.Status);
    }

    [Fact]
    public async Task UpdateImages_StoresValidImages_OfAnyAllowedFormat_InOrder()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        var dto = await service.UpdateImagesAsync(existing.Id, [Upload(Jpeg), Upload(Png), Upload(Webp)], []);

        Assert.Equal(3, dto.ImageIds.Count);
        Assert.Equal(3, _storage.Files.Count);
        Assert.Equal(["image/jpeg", "image/png", "image/webp"], existing.Images.OrderBy(i => i.SortOrder).Select(i => i.ContentType));
        Assert.Equal(existing.Images.OrderBy(i => i.SortOrder).Select(i => i.Id), dto.ImageIds);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateImages_RejectsMoreThanTen_AndSavesNothing()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var uploads = Enumerable.Range(0, Property.MaxImages + 1).Select(_ => Upload(Jpeg)).ToList();

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, uploads, []));

        Assert.Empty(existing.Images);
        Assert.Empty(_storage.Files);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateImages_AcceptsExactlyTen()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var uploads = Enumerable.Range(0, Property.MaxImages).Select(_ => Upload(Png)).ToList();

        var dto = await service.UpdateImagesAsync(existing.Id, uploads, []);

        Assert.Equal(Property.MaxImages, dto.ImageIds.Count);
    }

    [Fact]
    public async Task UpdateImages_CountsTheExistingOnes_AndLetsRemoveAndAddInOneStep()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var full = await service.UpdateImagesAsync(existing.Id, Enumerable.Range(0, Property.MaxImages).Select(_ => Upload(Jpeg)).ToList(), []);

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, [Upload(Png)], []));

        var swapped = await service.UpdateImagesAsync(existing.Id, [Upload(Png)], [full.ImageIds[0]]);

        Assert.Equal(Property.MaxImages, swapped.ImageIds.Count);
        Assert.DoesNotContain(full.ImageIds[0], swapped.ImageIds);
        Assert.Equal(Property.MaxImages, _storage.Files.Count); // el archivo quitado también se borró
    }

    [Fact]
    public async Task UpdateImages_CannotLeaveAPublishedPropertyWithoutImages()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var withImage = await service.UpdateImagesAsync(existing.Id, [Upload(Jpeg)], []);
        await service.UpdateAsync(existing.Id, PublishedUpdate());

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, [], withImage.ImageIds));

        Assert.Single(existing.Images);
    }

    [Fact]
    public async Task UpdateImages_RejectsUnknownImageIdToRemove()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, [], [Guid.CreateVersion7()]));
    }

    [Fact]
    public async Task UpdateImages_RejectsFilesThatAreNotRealImages_EvenIfTheyClaimToBe()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        byte[] script = [.. "<script>alert(1)</script>"u8];
        byte[] gif = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0, 0, 0, 0];

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, [Upload(Jpeg), Upload(script)], []));
        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, [Upload(gif)], []));
        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, [Upload([])], []));

        Assert.Empty(existing.Images);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task UpdateImages_RejectsImagesOverFiveMegabytes()
    {
        var existing = AddProperty(_agencyA.Id);
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));
        var big = new ImageUpload(new MemoryStream(Jpeg), PropertyImageRules.MaxBytes + 1);

        await Assert.ThrowsAsync<DomainException>(() => service.UpdateImagesAsync(existing.Id, [big], []));
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task UpdateImages_AsClient_IsForbidden()
    {
        var existing = AddProperty(_agencyB.Id);
        // El filtro por agencia vive en el repositorio real; aquí se comprueba el rol.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService(FakeCurrentUser.Client()).UpdateImagesAsync(existing.Id, [Upload(Jpeg)], []));
    }

    [Fact]
    public void ListQuery_Normalize_ClampsPagingAndTrimsSearch()
    {
        var normalized = new PropertyListQuery("  casa  ", Page: 0, PageSize: 5000).Normalize();

        Assert.Equal("casa", normalized.Search);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(PropertyListQuery.MaxPageSize, normalized.PageSize);
        Assert.Null(new PropertyListQuery("   ").Normalize().Search);
    }

    [Fact]
    public async Task ListQuery_NormalizesAndValidatesTheCriteria_BeforeReachingTheRepository()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await service.GetPagedAsync(new PropertyListQuery(Status: PropertyStatus.Draft, Amenities: [Amenity.Gym, Amenity.Gym], MinPrice: 5m, Currency: CurrencyCode.MXN));

        var sent = _properties.LastListQuery!;
        Assert.Equal(PropertyStatus.Draft, sent.Status);
        Assert.Equal([Amenity.Gym], sent.Amenities);

        await Assert.ThrowsAsync<DomainException>(() => service.GetPagedAsync(new PropertyListQuery(MinPrice: 5m)));
        await Assert.ThrowsAsync<DomainException>(() => service.GetPagedAsync(new PropertyListQuery(Status: (PropertyStatus)99)));
    }

    [Fact]
    public async Task Gallery_PassesTheCriteria_ButStaysInTheUsersAgencyAndIgnoresIds()
    {
        var service = CreateService(Staff(UserRole.Seller, _agencyA.Id));

        await service.GetGalleryAsync(new PublicPropertyQuery(MinBedrooms: 3, Ids: [Guid.NewGuid()], Sort: PropertySort.PriceAsc));

        Assert.Equal(_agencyA.Id, _properties.LastPublicAgencyId);
        Assert.Equal(3, _properties.LastPublicQuery!.MinBedrooms);
        Assert.Equal(PropertySort.PriceAsc, _properties.LastPublicQuery.Sort);
        Assert.Null(_properties.LastPublicQuery.Ids);
    }

    // --- Galería de la inmobiliaria ---------------------------------------------------------

    private Property AddPublished(Guid agencyId, string title)
    {
        var property = new Property(agencyId, PropertyTests.ValidData() with { Title = title, Status = PropertyStatus.Published });
        _properties.Items.Add(property);
        return property;
    }

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task Gallery_ShowsOnlyThePublishedPropertiesOfTheOwnAgency(UserRole role)
    {
        var mine = AddPublished(_agencyA.Id, "Publicada mía");
        AddProperty(_agencyA.Id, "Borrador mío");
        AddPublished(_agencyB.Id, "Publicada ajena");

        var page = await CreateService(Staff(role, _agencyA.Id)).GetGalleryAsync(new PublicPropertyQuery(Page: 1, PageSize: 12));

        Assert.Equal([mine.Id], page.Items.Select(p => p.Id));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Gallery_IsNotAvailableToThePlatformAdministratorNorToClients()
    {
        var platformAdmin = new FakeCurrentUser { UserId = Guid.CreateVersion7(), Role = UserRole.PlatformAdmin };

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(platformAdmin).GetGalleryAsync(new PublicPropertyQuery()));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService(FakeCurrentUser.Client()).GetGalleryAsync(new PublicPropertyQuery()));
    }

    [Fact]
    public async Task Gallery_ClampsThePaging()
    {
        AddPublished(_agencyA.Id, "Una");

        var page = await CreateService(Staff(UserRole.Seller, _agencyA.Id)).GetGalleryAsync(new PublicPropertyQuery(Page: -5, PageSize: 100_000));

        Assert.Equal(1, page.Page);
        Assert.Equal(PublicPropertyQuery.MaxPageSize, page.PageSize);
    }

    // --- Cuenta gratuita de un particular ---------------------------------------------------

    private async Task FillAsync(FakeCurrentUser owner, int count)
    {
        var service = CreateService(owner);
        for (var i = 0; i < count; i++)
        {
            await service.CreateAsync(NewRequest());
        }
    }

    [Fact]
    public async Task AFreeAccount_CanCreateUpToItsLimit_ButNotOneMore()
    {
        var individual = _agencies.AddIndividual();
        var owner = Staff(UserRole.AgencyAdmin, individual.Id);
        await FillAsync(owner, 3);

        var ex = await Assert.ThrowsAsync<PlanLimitException>(() => CreateService(owner).CreateAsync(NewRequest()));

        Assert.Contains("3 propiedades", ex.Message);
        Assert.Equal(3, _properties.Items.Count(p => p.AgencyId == individual.Id));
    }

    [Fact]
    public async Task AFreeAccount_RecoversRoomWhenItDeletesAProperty()
    {
        var individual = _agencies.AddIndividual();
        var owner = Staff(UserRole.AgencyAdmin, individual.Id);
        await FillAsync(owner, 3);
        var service = CreateService(owner);
        await service.DeleteAsync(_properties.Items.First(p => p.AgencyId == individual.Id).Id);

        await service.CreateAsync(NewRequest());

        Assert.Equal(3, _properties.Items.Count(p => p.AgencyId == individual.Id && !p.IsDeleted));
    }

    [Fact]
    public async Task ARegularAgency_HasNoPropertyLimit()
    {
        var owner = Staff(UserRole.AgencyAdmin, _agencyA.Id);

        await FillAsync(owner, 5);

        Assert.Equal(5, _properties.Items.Count(p => p.AgencyId == _agencyA.Id));
    }

    [Fact]
    public async Task TheLimitIsPerAccount_AnotherAccountStartsFromZero()
    {
        var first = _agencies.AddIndividual("Uno");
        var second = _agencies.AddIndividual("Dos");
        await FillAsync(Staff(UserRole.AgencyAdmin, first.Id), 3);

        await CreateService(Staff(UserRole.AgencyAdmin, second.Id)).CreateAsync(NewRequest());

        Assert.Single(_properties.Items, p => p.AgencyId == second.Id);
    }}
