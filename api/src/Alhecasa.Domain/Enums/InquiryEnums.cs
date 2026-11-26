namespace Alhecasa.Domain.Enums;

/// <summary>Etapa de una solicitud de informes dentro del seguimiento de la inmobiliaria.</summary>
public enum InquiryStatus
{
    /// <summary>Recién llegada, nadie la ha atendido.</summary>
    New = 1,

    /// <summary>Ya se respondió o se está dando seguimiento.</summary>
    InProgress = 2,

    /// <summary>Atendida por completo (la venta o renta se concretó o el prospecto ya no está interesado).</summary>
    Closed = 3
}

/// <summary>Tipo de anotación en el seguimiento de una solicitud.</summary>
public enum FollowUpKind
{
    /// <summary>Respuesta dada al prospecto (queda registrada lo que se le contestó).</summary>
    Reply = 1,

    /// <summary>Nota interna: solo la ve el personal de la inmobiliaria.</summary>
    Note = 2
}