using Alhecasa.Domain.Common;
using Alhecasa.Domain.Enums;

namespace Alhecasa.Domain.Entities;

/// <summary>
/// Una anotación en el seguimiento de una solicitud de informes: una respuesta al prospecto o una nota interna.
/// Se crea únicamente desde <see cref="Inquiry"/>.
/// </summary>
public class InquiryFollowUp : BaseEntity
{
    private InquiryFollowUp()
    {
    }

    internal InquiryFollowUp(Guid inquiryId, Guid agencyId, FollowUpKind kind, string text, Guid authorId, string authorName)
    {
        InquiryId = inquiryId;
        AgencyId = agencyId;
        Kind = kind;
        Text = text;
        AuthorId = authorId;
        AuthorName = authorName;
    }

    public Guid InquiryId { get; private set; }

    /// <summary>Inmobiliaria dueña (la de la solicitud): permite aislar por tenant sin cruzar tablas.</summary>
    public Guid AgencyId { get; private set; }

    public FollowUpKind Kind { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public Guid AuthorId { get; private set; }

    /// <summary>Nombre de quien lo escribió al momento de escribirlo: el historial no cambia si luego se renombra o se da de baja al usuario.</summary>
    public string AuthorName { get; private set; } = string.Empty;
}