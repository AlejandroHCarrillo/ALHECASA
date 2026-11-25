namespace Alhecasa.Application.Common;

/// <summary>Topes de las cuentas gratuitas. Se configuran en <c>AccountLimits:IndividualMaxProperties</c>.</summary>
public sealed record AccountLimits(int IndividualMaxProperties = 3);