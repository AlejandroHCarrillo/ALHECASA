namespace Alhecasa.Domain.Enums;

public enum PropertyType
{
    House = 1,
    Apartment = 2,
    Land = 3,
    Commercial = 4,
    Office = 5
}

/// <summary>Operación comercial con la que se ofrece la propiedad.</summary>
public enum OperationType
{
    Sale = 1,
    Rent = 2
}

/// <summary>Estado de publicación de la propiedad dentro del inventario de la inmobiliaria.</summary>
public enum PropertyStatus
{
    /// <summary>Capturada, todavía no visible.</summary>
    Draft = 1,

    /// <summary>Publicada: visible para los interesados.</summary>
    Published = 2,

    /// <summary>Retirada temporalmente.</summary>
    Paused = 3,

    /// <summary>Operación concluida (vendida o rentada).</summary>
    Closed = 4
}

public enum CurrencyCode
{
    MXN = 1,
    USD = 2
}
