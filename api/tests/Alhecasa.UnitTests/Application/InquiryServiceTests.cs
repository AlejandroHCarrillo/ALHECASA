using Alhecasa.Application.Common;
using Alhecasa.Application.Inquiries;
using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;
using Alhecasa.UnitTests.Domain;
using Alhecasa.UnitTests.Support;
using FluentValidation;

namespace Alhecasa.UnitTests.Application;

public sealed class InquiryServiceTests
{
    private readonly FakePropertyRepository _properties = new();
    private readonly FakeInquiryRepository _inquiries = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _agencyId = Guid.CreateVersion7();

    private static CreateInquiryRequest ValidRequest() => new()
    {
        Name = "Ana López",
        Phone = "+52 (55) 1234-5678",
        Email = "Ana@Correo.com",
        Message = "¿Sigue disponible?"
    };

    private PublicInquiryService CreatePublicService() =>
        new(_properties, _inquiries, _unitOfWork, new CreateInquiryRequestValidator());

    private Property AddProperty(PropertyStatus status, Guid? agencyId = null)
    {
        var property = new Property(agencyId ?? _agencyId, PropertyTests.ValidData() with { Title = "Casa en Polanco", Status = status });
        _properties.Items.Add(property);
        return property;
    }

    private (InquiryService Service, User Staff) CreateStaffService(UserRole role = UserRole.Seller)
    {
        var staff = new User("Luis", "Vendedor", $"{Guid.NewGuid():N}@a.test", "hash", role, _agencyId);
        _users.Users.Add(staff);
        var currentUser = new FakeCurrentUser { UserId = staff.Id, Role = role, AgencyId = _agencyId };
        var service = new InquiryService(
            _inquiries,
            _users,
            _unitOfWork,
            currentUser,
            new AddFollowUpRequestValidator(),
            new ChangeInquiryStatusRequestValidator());
        return (service, staff);
    }

    private Inquiry AddInquiry()
    {
        var inquiry = new Inquiry(Guid.CreateVersion7(), _agencyId, "Casa", "Ana", "5512345678", "ana@a.com", "Hola");
        _inquiries.Items.Add(inquiry);
        return inquiry;
    }

    // --- Alta pública -----------------------------------------------------------------------

    [Fact]
    public async Task Submit_StoresTheInquiryForTheAgencyThatOwnsTheProperty()
    {
        var property = AddProperty(PropertyStatus.Published);

        await CreatePublicService().SubmitAsync(property.Id, ValidRequest());

        var stored = Assert.Single(_inquiries.Items);
        Assert.Equal(property.Id, stored.PropertyId);
        Assert.Equal(_agencyId, stored.AgencyId);
        Assert.Equal("Casa en Polanco", stored.PropertyTitle);
        Assert.Equal("ana@correo.com", stored.Email);
        Assert.Equal(InquiryStatus.New, stored.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Paused)]
    [InlineData(PropertyStatus.Closed)]
    public async Task Submit_ForAPropertyThatIsNotPublished_IsNotFound(PropertyStatus status)
    {
        var property = AddProperty(status);

        await Assert.ThrowsAsync<NotFoundException>(() => CreatePublicService().SubmitAsync(property.Id, ValidRequest()));
        Assert.Empty(_inquiries.Items);
    }

    [Fact]
    public async Task Submit_ForAnUnknownProperty_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreatePublicService().SubmitAsync(Guid.NewGuid(), ValidRequest()));
    }

    [Fact]
    public async Task Submit_WithInvalidData_StoresNothing()
    {
        var property = AddProperty(PropertyStatus.Published);

        await Assert.ThrowsAsync<ValidationException>(() => CreatePublicService().SubmitAsync(property.Id, ValidRequest() with { Email = "no-es-correo" }));
        Assert.Empty(_inquiries.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // --- Validación -------------------------------------------------------------------------

    [Theory]
    [InlineData("5512345678", true)]
    [InlineData("+52 55 1234 5678", true)]
    [InlineData("(55) 1234-5678", true)]
    [InlineData("55.1234.5678", true)]
    [InlineData("123456", false)]
    [InlineData("1234567890123456", false)]
    [InlineData("55-ABCD-5678", false)]
    [InlineData("55 1234 5678 ext 4", false)]
    [InlineData("", false)]
    public void Phone_MustHaveSevenToFifteenDigitsAndOnlyPhoneCharacters(string phone, bool valid)
    {
        var result = new CreateInquiryRequestValidator().Validate(ValidRequest() with { Phone = phone });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Validator_RejectsBlankAndOverlongFields()
    {
        var validator = new CreateInquiryRequestValidator();

        Assert.False(validator.Validate(ValidRequest() with { Name = " " }).IsValid);
        Assert.False(validator.Validate(ValidRequest() with { Name = new string('a', InquiryLimits.Name + 1) }).IsValid);
        Assert.False(validator.Validate(ValidRequest() with { Message = "" }).IsValid);
        Assert.False(validator.Validate(ValidRequest() with { Message = new string('a', InquiryLimits.Text + 1) }).IsValid);
        Assert.True(validator.Validate(ValidRequest() with { Message = new string('a', InquiryLimits.Text) }).IsValid);
    }

    // --- Bandeja del personal ---------------------------------------------------------------

    [Theory]
    [InlineData(UserRole.AgencyAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Seller)]
    public async Task AgencyStaff_CanListAndOpenInquiries(UserRole role)
    {
        var (service, _) = CreateStaffService(role);
        var inquiry = AddInquiry();

        var page = await service.GetPagedAsync(new InquiryListQuery());
        var detail = await service.GetByIdAsync(inquiry.Id);

        Assert.Equal([inquiry.Id], page.Items.Select(i => i.Id));
        Assert.Equal("Ana", detail.Name);
        Assert.Equal("5512345678", detail.Phone);
    }

    [Fact]
    public async Task ThePlatformAdministratorAndClients_CannotHandleInquiries()
    {
        var inquiry = AddInquiry();
        var platformAdmin = new FakeCurrentUser { UserId = Guid.CreateVersion7(), Role = UserRole.PlatformAdmin };

        foreach (var user in new[] { platformAdmin, FakeCurrentUser.Client() })
        {
            var service = new InquiryService(_inquiries, _users, _unitOfWork, user, new AddFollowUpRequestValidator(), new ChangeInquiryStatusRequestValidator());

            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetPagedAsync(new InquiryListQuery()));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(inquiry.Id));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Text = "Hola" }));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.ChangeStatusAsync(inquiry.Id, new ChangeInquiryStatusRequest { Status = InquiryStatus.Closed }));
        }
    }

    [Fact]
    public async Task GetById_OfAnUnknownInquiry_IsNotFound()
    {
        var (service, _) = CreateStaffService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AReply_IsSignedWithTheStaffMember_AndMovesTheInquiryToInProgress()
    {
        var (service, staff) = CreateStaffService();
        var inquiry = AddInquiry();

        var updated = await service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Kind = FollowUpKind.Reply, Text = "Claro, la puede visitar el sábado." });

        Assert.Equal(InquiryStatus.InProgress, updated.Status);
        var entry = Assert.Single(updated.FollowUps);
        Assert.Equal(FollowUpKind.Reply, entry.Kind);
        Assert.Equal("Claro, la puede visitar el sábado.", entry.Text);
        Assert.Equal(staff.FullName, entry.AuthorName);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ANote_KeepsTheStatus()
    {
        var (service, _) = CreateStaffService();
        var inquiry = AddInquiry();

        var updated = await service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Kind = FollowUpKind.Note, Text = "Pidió que le llamen en la tarde" });

        Assert.Equal(InquiryStatus.New, updated.Status);
    }

    [Fact]
    public async Task FollowUp_WithBlankOrOverlongText_IsRejected()
    {
        var (service, _) = CreateStaffService();
        var inquiry = AddInquiry();

        await Assert.ThrowsAsync<ValidationException>(() => service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Text = "  " }));
        await Assert.ThrowsAsync<ValidationException>(() => service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Text = new string('a', InquiryLimits.Text + 1) }));
        await Assert.ThrowsAsync<ValidationException>(() => service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Kind = (FollowUpKind)99, Text = "Hola" }));
        Assert.Empty(inquiry.FollowUps);
    }

    [Fact]
    public async Task FollowUps_AreListedOldestFirst()
    {
        var (service, _) = CreateStaffService();
        var inquiry = AddInquiry();

        await service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Text = "Primera" });
        var updated = await service.AddFollowUpAsync(inquiry.Id, new AddFollowUpRequest { Kind = FollowUpKind.Note, Text = "Segunda" });

        Assert.Equal(["Primera", "Segunda"], updated.FollowUps.Select(f => f.Text));
    }

    [Fact]
    public async Task ChangeStatus_UpdatesTheInquiry_AndRejectsUnknownValues()
    {
        var (service, _) = CreateStaffService();
        var inquiry = AddInquiry();

        var closed = await service.ChangeStatusAsync(inquiry.Id, new ChangeInquiryStatusRequest { Status = InquiryStatus.Closed });

        Assert.Equal(InquiryStatus.Closed, closed.Status);
        await Assert.ThrowsAsync<ValidationException>(() => service.ChangeStatusAsync(inquiry.Id, new ChangeInquiryStatusRequest { Status = (InquiryStatus)99 }));
    }

    [Fact]
    public void ListQuery_NormalizesSearchAndPaging()
    {
        var query = new InquiryListQuery(Search: "  ana ", Page: -3, PageSize: 100_000).Normalize();

        Assert.Equal("ana", query.Search);
        Assert.Equal(1, query.Page);
        Assert.Equal(InquiryListQuery.MaxPageSize, query.PageSize);
        Assert.Null(new InquiryListQuery(Search: "   ").Normalize().Search);
    }
}