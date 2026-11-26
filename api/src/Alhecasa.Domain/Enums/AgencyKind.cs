namespace Alhecasa.Domain.Enums;

/// <summary>Qué representa una "inmobiliaria" (el límite de aislamiento de datos).</summary>
public enum AgencyKind
{
    /// <summary>Una inmobiliaria o empresa, con personal propio.</summary>
    Company = 1,

    /// <summary>Un particular con cuenta gratuita: un solo usuario y un tope de propiedades.</summary>
    Individual = 2
}