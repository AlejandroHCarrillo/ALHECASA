namespace Alhecasa.Domain.Enums;

/// <summary>Qué otorga un plan.</summary>
public enum PlanKind
{
    /// <summary>Un tope de propiedades que la cuenta puede tener (publicaciones).</summary>
    Listings = 1,

    /// <summary>Un paquete de anuncios destacados: cuántos pueden estar destacados a la vez mientras dura el paquete.</summary>
    Featured = 2
}

/// <summary>Duración de una contratación. Una contratación dura exactamente un periodo.</summary>
public enum BillingPeriod
{
    Day = 1,
    Week = 2,
    Month = 3,
    Year = 4
}

/// <summary>Qué tipo de cuenta puede contratar el plan.</summary>
public enum PlanAudience
{
    /// <summary>Cuentas de particulares (gratuitas).</summary>
    Individual = 1,

    /// <summary>Inmobiliarias.</summary>
    Company = 2,

    /// <summary>Cualquier cuenta.</summary>
    Any = 3
}
