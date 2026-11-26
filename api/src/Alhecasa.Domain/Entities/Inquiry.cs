using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Domain.Entities;

/// <summary>
/// Solicitud de informes de un prospecto sobre una propiedad publicada. La manda un visitante sin sesión;
/// la inmobiliaria dueña de la propiedad la atiende y le da seguimiento.
/// </summary>
public class Inquiry : BaseEntity
{
    public const int MaxFollowUps = 200;

    private readonly List<InquiryFollowUp> _followUps = [];

    private Inquiry()
    {
    }

    public Inquiry(Guid propertyId, Guid agencyId, string propertyTitle, string name, string phone, string email, string message)
    {
        if (propertyId == Guid.Empty || agencyId == Guid.Empty)
        {
            throw new DomainException("An inquiry needs a property and an agency.");
        }

        PropertyId = propertyId;
        AgencyId = agencyId;
        PropertyTitle = Required(propertyTitle, "property title");
        Name = Required(name, "name");
        Phone = Required(phone, "phone");
        Email = Required(email, "email").ToLowerInvariant();
        Message = Required(message, "message");
    }

    public Guid PropertyId { get; private set; }

    /// <summary>Inmobiliaria dueña de la propiedad al momento de la solicitud: aísla por tenant.</summary>
    public Guid AgencyId { get; private set; }

    /// <summary>Título de la propiedad al momento de la solicitud: sigue legible aunque luego se renombre o se elimine la propiedad.</summary>
    public string PropertyTitle { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Phone { get; private set; } = string.Empty;

    /// <summary>Correo del prospecto, en minúsculas.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>La pregunta del prospecto.</summary>
    public string Message { get; private set; } = string.Empty;

    public InquiryStatus Status { get; private set; } = InquiryStatus.New;

    public IReadOnlyCollection<InquiryFollowUp> FollowUps => _followUps;

    /// <summary>
    /// Agrega una respuesta o una nota. La primera respuesta a una solicitud nueva (o a una cerrada) la pasa a «En seguimiento»;
    /// una nota no cambia la etapa.
    /// </summary>
    public InquiryFollowUp AddFollowUp(FollowUpKind kind, string text, Guid authorId, string authorName)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Unknown follow-up kind.");
        }

        if (authorId == Guid.Empty)
        {
            throw new DomainException("A follow-up needs an author.");
        }

        if (_followUps.Count >= MaxFollowUps)
        {
            throw new DomainException($"An inquiry can have at most {MaxFollowUps} follow-up entries.");
        }

        var followUp = new InquiryFollowUp(Id, AgencyId, kind, Required(text, "text"), authorId, Required(authorName, "author name"));
        _followUps.Add(followUp);

        if (kind == FollowUpKind.Reply && Status != InquiryStatus.InProgress)
        {
            Status = InquiryStatus.InProgress;
        }

        return followUp;
    }

    public void ChangeStatus(InquiryStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainException("Unknown inquiry status.");
        }

        Status = status;
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw new DomainException($"The {field} is required.") : value.Trim();
}