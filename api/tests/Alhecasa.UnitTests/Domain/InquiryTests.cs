using Alhecasa.Domain.Common;
using Alhecasa.Domain.Entities;
using Alhecasa.Domain.Enums;

namespace Alhecasa.UnitTests.Domain;

public sealed class InquiryTests
{
    private static readonly Guid AgencyId = Guid.CreateVersion7();
    private static readonly Guid PropertyId = Guid.CreateVersion7();
    private static readonly Guid AuthorId = Guid.CreateVersion7();

    private static Inquiry NewInquiry() =>
        new(PropertyId, AgencyId, "Casa en Polanco", "  Ana López ", " 55 1234 5678 ", "  ANA@Correo.COM ", " ¿Sigue disponible? ");

    [Fact]
    public void NewInquiry_StartsAsNew_WithTrimmedData_AndALowercaseEmail()
    {
        var inquiry = NewInquiry();

        Assert.Equal(InquiryStatus.New, inquiry.Status);
        Assert.Equal("Ana López", inquiry.Name);
        Assert.Equal("55 1234 5678", inquiry.Phone);
        Assert.Equal("ana@correo.com", inquiry.Email);
        Assert.Equal("¿Sigue disponible?", inquiry.Message);
        Assert.Equal(AgencyId, inquiry.AgencyId);
        Assert.Equal(PropertyId, inquiry.PropertyId);
        Assert.Equal("Casa en Polanco", inquiry.PropertyTitle);
        Assert.Empty(inquiry.FollowUps);
        Assert.Equal(7, inquiry.Id.Version);
    }

    [Theory]
    [InlineData("", "555", "a@a.com", "hola")]
    [InlineData("Ana", " ", "a@a.com", "hola")]
    [InlineData("Ana", "555", "", "hola")]
    [InlineData("Ana", "555", "a@a.com", "   ")]
    public void RequiredFields_AreEnforced(string name, string phone, string email, string message)
    {
        Assert.Throws<DomainException>(() => new Inquiry(PropertyId, AgencyId, "Casa", name, phone, email, message));
    }

    [Fact]
    public void AnInquiryNeedsAPropertyAndAnAgency()
    {
        Assert.Throws<DomainException>(() => new Inquiry(Guid.Empty, AgencyId, "Casa", "Ana", "555", "a@a.com", "hola"));
        Assert.Throws<DomainException>(() => new Inquiry(PropertyId, Guid.Empty, "Casa", "Ana", "555", "a@a.com", "hola"));
    }

    [Fact]
    public void AReply_MovesANewInquiryToInProgress_AndIsRecordedWithItsAuthor()
    {
        var inquiry = NewInquiry();

        var followUp = inquiry.AddFollowUp(FollowUpKind.Reply, " Sí, sigue disponible. ", AuthorId, "Luis Vendedor");

        Assert.Equal(InquiryStatus.InProgress, inquiry.Status);
        Assert.Same(followUp, Assert.Single(inquiry.FollowUps));
        Assert.Equal("Sí, sigue disponible.", followUp.Text);
        Assert.Equal(FollowUpKind.Reply, followUp.Kind);
        Assert.Equal(AuthorId, followUp.AuthorId);
        Assert.Equal("Luis Vendedor", followUp.AuthorName);
        Assert.Equal(inquiry.Id, followUp.InquiryId);
        Assert.Equal(AgencyId, followUp.AgencyId);
    }

    [Fact]
    public void ANote_DoesNotChangeTheStatus()
    {
        var inquiry = NewInquiry();

        inquiry.AddFollowUp(FollowUpKind.Note, "Llamar el lunes", AuthorId, "Luis");

        Assert.Equal(InquiryStatus.New, inquiry.Status);
    }

    [Fact]
    public void AReply_ReopensAClosedInquiry()
    {
        var inquiry = NewInquiry();
        inquiry.ChangeStatus(InquiryStatus.Closed);

        inquiry.AddFollowUp(FollowUpKind.Reply, "Retomamos el tema", AuthorId, "Luis");

        Assert.Equal(InquiryStatus.InProgress, inquiry.Status);
    }

    [Fact]
    public void FollowUp_RequiresTextAndAuthor()
    {
        var inquiry = NewInquiry();

        Assert.Throws<DomainException>(() => inquiry.AddFollowUp(FollowUpKind.Reply, " ", AuthorId, "Luis"));
        Assert.Throws<DomainException>(() => inquiry.AddFollowUp(FollowUpKind.Reply, "Hola", Guid.Empty, "Luis"));
        Assert.Throws<DomainException>(() => inquiry.AddFollowUp(FollowUpKind.Reply, "Hola", AuthorId, ""));
        Assert.Throws<DomainException>(() => inquiry.AddFollowUp((FollowUpKind)99, "Hola", AuthorId, "Luis"));
        Assert.Empty(inquiry.FollowUps);
        Assert.Equal(InquiryStatus.New, inquiry.Status);
    }

    [Fact]
    public void FollowUps_AreCapped()
    {
        var inquiry = NewInquiry();
        for (var i = 0; i < Inquiry.MaxFollowUps; i++)
        {
            inquiry.AddFollowUp(FollowUpKind.Note, $"Nota {i}", AuthorId, "Luis");
        }

        Assert.Throws<DomainException>(() => inquiry.AddFollowUp(FollowUpKind.Note, "Una más", AuthorId, "Luis"));
    }

    [Theory]
    [InlineData(InquiryStatus.InProgress)]
    [InlineData(InquiryStatus.Closed)]
    [InlineData(InquiryStatus.New)]
    public void ChangeStatus_AcceptsAnyKnownStatus(InquiryStatus status)
    {
        var inquiry = NewInquiry();

        inquiry.ChangeStatus(status);

        Assert.Equal(status, inquiry.Status);
    }

    [Fact]
    public void ChangeStatus_RejectsAnUnknownStatus()
    {
        Assert.Throws<DomainException>(() => NewInquiry().ChangeStatus((InquiryStatus)99));
    }
}